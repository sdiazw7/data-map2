[← Index](../specs.md)

## 1. Session Authentication

All protected API requests require a valid session cookie.

**Cookie name:** `participant_session`
**Session duration:** 30 days, rolling expiration

Each authenticated request must update `participant_sessions.last_seen_at`. If `last_seen_at` is older than 30 days, the session is expired and the middleware returns `401 Unauthorized`.

**Middleware responsibilities:**
1. Read cookie
2. Look up `participant_sessions` table
3. Verify session not expired
4. Update `last_seen_at`
5. Attach `participant_id` and `workspace_id` to request context

**Client-side session state:** on a successful join, the frontend also stores the `JoinResponse` (`participantId`, `workspaceId`, `workspaceName`, `email`) in `localStorage` under the key `datamap_session` via `useSession`, and sends `credentials: 'include'` on every request. The cookie remains the source of truth for authorization — `localStorage` only lets the UI know who is "logged in" without re-fetching.

## 2. Invite Access Flow

**Invite URL:** `/invite/{token}`

```
User opens invite
  → Invite validated
  → Email collected
  → Participant upserted (see [§3](#3-participant-upsert-and-invite-usage-rules) — behavior differs for template invites)
  → Session cookie issued
  → Workspace opened
```

## 2a. Dev Access (Development Only)

When `ASPNETCORE_ENVIRONMENT=Development`, two additional endpoints are mapped (`DevEndpoints`, only registered when `app.Environment.IsDevelopment()`) that skip the invite flow entirely:

```
GET  /dev/workspaces
POST /dev/workspaces/{id}/join
```

`GET /dev/workspaces` lists all workspaces (`WorkspaceSummaryDto[]`) so a developer can pick one from a workspace picker in the UI. `POST /dev/workspaces/{id}/join` logs in as a standing dev participant (`dev@local`) for that workspace: it mints (or reuses) a per-workspace invite token `dev-{workspaceId}` to satisfy the required `Participant.InviteId` foreign key, then issues a normal `participant_session` cookie exactly like the real invite flow.

This exists purely for local development convenience and must never be reachable outside `Development`.

**Frontend:** `AppHeader` renders a "Switch workspace" link back to `/` only when `import.meta.env.DEV` is true.

## 3. Participant Upsert and Invite Usage Rules

**Endpoint:** `POST /invite/{token}/join`

Participants are unique by `(workspace_id, email)`.

An invite is either **shared** or **template**, based on whether `invites.template_workspace_id` is set (see [§2b](#2b-invite-creation) for how these are created).

### Shared invite (`template_workspace_id` is null)

If a participant with the same email already exists in the invite's workspace:
- → reuse the existing participant
- → update `last_seen_at`
- → DO NOT increment `invites.used_count`

If no participant exists:
- → create a new participant in the invite's workspace
- → increment `invites.used_count`

### Template invite (`template_workspace_id` is set)

Every participant gets their **own private copy** of the template workspace instead of sharing one:

- **Returning user** (a workspace exists with `source_template_id = template_workspace_id` and a participant with this email): reuse that workspace and participant, update `last_seen_at`, do not increment `used_count`.
- **New user**: call `WorkspaceCopyService.CopyAsync` to deep-copy the template workspace (schemas → tables → columns, with fresh IDs, then a projection refresh), create a participant in the new workspace, and increment `used_count`.

This ensures returning users keep their history while invite usage limits still function correctly, and — for template invites — that each participant edits their own copy without stepping on anyone else's data.

### Atomicity

A join spans several writes — workspace copy, participant, `used_count`, session — and they commit as
a single transaction ([§1, Backend Architecture](02-backend-architecture.md#1-backend-architecture)).

This matters most for a template invite, where the copy is created before the participant. A copied
workspace is only ever found again by looking up a participant with that email against
`source_template_id`, so a failure between those two writes would strand the copy with nothing
pointing at it — and the user would be handed a fresh copy on every retry, accumulating orphans.

## 2b. Invite Creation

**Endpoint:** `POST /invites` (protected — requires an active session; the workspace is taken from the caller's session, not the request body)

**Request:**
```json
{
  "maxUses": 50,
  "expiresAt": "2027-01-01T00:00:00Z",
  "templateWorkspaceId": null
}
```

| Field | Required | Description |
|---|---|---|
| `maxUses` | Yes | How many times the link can be used. Between 1 and 1000. |
| `expiresAt` | Yes | UTC datetime after which the link is no longer valid. Must be in the future, and no more than 365 days out. |
| `templateWorkspaceId` | No | If set, each new user gets their own copy of this workspace (see [§3](#3-participant-upsert-and-invite-usage-rules)) instead of joining a shared one. Subject to the authorization rule below. |

`maxUses` and `expiresAt` are bounded rather than left open because an invite link is the only
access control in the product ([§2, Non-Functional](09-non-functional.md#2-non-goals-mvp) rules out
RBAC). An invite with an unbounded use count and a distant expiry is a permanent open door.

**Template authorization.** A caller may only build a template invite around a template they are
actually working in — the template workspace itself, or a workspace copied from it
(`source_template_id` matches). Any other target fails with `404`
(`TemplateWorkspaceNotFoundException`), as does a target that does not exist or is not flagged
`is_template`.

All three cases return the same `404` deliberately: a distinct "forbidden" response would let a
caller probe workspace ids and learn which ones are real templates.

**Response (`201 Created`):**
```json
{
  "id": "...",
  "token": "aB3xQ7...",
  "workspaceId": "...",
  "expiresAt": "2027-01-01T00:00:00Z",
  "maxUses": 50,
  "templateWorkspaceId": null
}
```

`token` is a 32-byte cryptographically random value, base64url-encoded. Share it as `/invite/{token}`.

## 4. Invite Validation Rules

Invite invalid if:
```
expires_at < now()
OR
used_count >= max_uses
```

Both conditions mean the same thing to the person holding the link — it is dead and no action of
theirs revives it — so both return the same status. They stay distinguishable through `error.code`,
which is what the UI branches on to choose its wording.

**Errors**

| Status | `error.code` | Case |
|---|---|---|
| `404` | `INVITE_NOT_FOUND` | no invite with that token |
| `410` | `INVITE_EXPIRED` | `expires_at < now()` |
| `410` | `INVITE_USAGE_EXCEEDED` | `used_count >= max_uses` |
| `404` | `TEMPLATE_WORKSPACE_NOT_FOUND` | template missing, not a template, or not one the caller may use (invite creation only — see [§2b](#2b-invite-creation)) |

`410 Gone` rather than `429 Too Many Requests`: `429` describes a caller sending requests too
quickly and invites a retry after a delay, but an exhausted invite is a permanent property of the
invite — waiting does not help, and a different person gets the same answer. Clients and proxies
routinely auto-retry `429`, so using it here produces retry traffic against a link that will never
work again.

`409 Conflict` is likewise reserved for conflicts the caller *can* resolve and retry — a stale
row version, a business term name already taken. An exhausted invite is not one of those.
