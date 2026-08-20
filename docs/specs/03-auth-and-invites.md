[← Index](../specs.md)

## 5. Session Authentication

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

## 6. Invite Access Flow

**Invite URL:** `/invite/{token}`

```
User opens invite
  → Invite validated
  → Email collected
  → Participant upserted (see [§7](#7-participant-upsert-and-invite-usage-rules) — behavior differs for template invites)
  → Session cookie issued
  → Workspace opened
```

## 6a. Dev Access (Development Only)

When `ASPNETCORE_ENVIRONMENT=Development`, two additional endpoints are mapped (`DevEndpoints`, only registered when `app.Environment.IsDevelopment()`) that skip the invite flow entirely:

```
GET  /dev/workspaces
POST /dev/workspaces/{id}/join
```

`GET /dev/workspaces` lists all workspaces (`WorkspaceSummaryDto[]`) so a developer can pick one from a workspace picker in the UI. `POST /dev/workspaces/{id}/join` logs in as a standing dev participant (`dev@local`) for that workspace: it mints (or reuses) a per-workspace invite token `dev-{workspaceId}` to satisfy the required `Participant.InviteId` foreign key, then issues a normal `participant_session` cookie exactly like the real invite flow.

This exists purely for local development convenience and must never be reachable outside `Development`.

**Frontend:** `AppHeader` renders a "Switch workspace" link back to `/` only when `import.meta.env.DEV` is true.

## 7. Participant Upsert and Invite Usage Rules

**Endpoint:** `POST /invite/{token}/join`

Participants are unique by `(workspace_id, email)`.

An invite is either **shared** or **template**, based on whether `invites.template_workspace_id` is set (see [§6b](#6b-invite-creation) for how these are created).

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

## 6b. Invite Creation

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
| `maxUses` | Yes | How many times the link can be used (minimum 1). |
| `expiresAt` | Yes | UTC datetime after which the link is no longer valid. Must be in the future. |
| `templateWorkspaceId` | No | If set, each new user gets their own copy of this workspace (see [§7](#7-participant-upsert-and-invite-usage-rules)) instead of joining a shared one. The referenced workspace must exist and have `is_template = true`, or the request fails with `404` (`TemplateWorkspaceNotFoundException`). |

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

## 8. Invite Validation Rules

Invite invalid if:
```
expires_at < now()
OR
used_count >= max_uses
```

**Errors**
- `404` → invite not found
- `410` → invite expired
- `429` → invite usage exceeded
- `404` → template workspace not found / not a template (invite creation only)
