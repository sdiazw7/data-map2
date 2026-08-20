# Build Specification — Spreadsheet-First Metadata Catalog

## 1. Product Goal

Build a lightweight metadata catalog that lets invited users:

- Open a workspace via invite link
- Explore a demo dataset
- Upload metadata via CSV
- Document tables and columns in a spreadsheet-style editor
- Define business terms
- Track documentation coverage

The system must scale to 100k+ metadata rows.

MVP constraints:

- No authentication providers
- No external database connectors
- Access via invite links only (dev-only convenience login is the sole exception — see [§6a](#6a-dev-access-development-only))

## 2. Technology Stack

**Frontend**
- React
- TypeScript
- Vite
- Tailwind CSS
- TanStack Table
- `@tanstack/react-virtual` — row virtualization for the metadata grid, so 100k+ rows don't bloat the DOM

**Backend**
- ASP.NET Core
- Minimal APIs

**Database**
- PostgreSQL

## 3. Architecture Principles

- Use explicit relational tables
- Avoid generic asset graphs
- Use UUID primary keys
- Add foreign keys and indexes
- Avoid JSON metadata blobs
- Separate read models and write models
- Optimize for fast metadata grid queries

## 4. Backend Architecture

**Request Flow**

```
Endpoints → Services → Repositories → Database
```

- **Endpoints** handle HTTP requests and validation.
- **Services** contain business logic and orchestration.
- **Repositories** handle database queries only.

`SessionAuthMiddleware` sits in front of every endpoint except `GET /health` and `GET|POST /invite/*`, and populates `HttpContext.Items["ParticipantId"]` / `["WorkspaceId"]` for downstream use.

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
  → Participant upserted (see §7 — behavior differs for template invites)
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

An invite is either **shared** or **template**, based on whether `invites.template_workspace_id` is set (see §6b for how these are created).

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
| `templateWorkspaceId` | No | If set, each new user gets their own copy of this workspace (see §7) instead of joining a shared one. The referenced workspace must exist and have `is_template = true`, or the request fails with `404` (`TemplateWorkspaceNotFoundException`). |

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

## 9. Database Schema

**workspaces**
- id
- name
- created_at
- is_template — marks a workspace as a copy source for template invites
- source_template_id — nullable; set on workspaces created by `WorkspaceCopyService`, pointing back at the template they were copied from

**invites**
- id
- workspace_id
- token
- created_at
- expires_at
- max_uses
- used_count
- template_workspace_id — nullable; presence makes this a template invite (see §7)

**participants**
- id
- workspace_id
- email
- invite_id
- created_at
- last_seen_at

Unique constraint: `(workspace_id, email)`

**participant_sessions**
- id
- participant_id
- workspace_id
- created_at
- last_seen_at

**schemas**
- id
- workspace_id
- name

**tables**
- id
- workspace_id
- schema_id
- name
- description
- created_at
- updated_at

**columns**
- id
- workspace_id
- table_id
- name
- data_type
- example_value
- description
- owner
- version
- created_at
- updated_at

Column `version` is used for optimistic concurrency control. `PATCH` requests must include the current version. If a version mismatch occurs → return `409 Conflict`.

**relationships**
- id
- workspace_id
- source_column_id
- target_column_id
- relationship_type

**business_terms**
- id
- workspace_id
- name
- definition

**term_column_mapping**
- id
- term_id
- column_id

**metadata_changes**
- id
- entity_type
- entity_id
- field
- old_value
- new_value
- participant_id
- edited_at

## 10. Projection Table (Read Model)

**Table name:** `column_catalog_editor`

**Purpose:** provide fast spreadsheet grid queries. This table is NOT a materialized view — it is a writable projection table maintained by the application.

**Fields**
- workspace_id
- column_id
- schema_name
- table_name
- column_name
- data_type
- example_value
- description
- business_term
- owner
- version

## 11. Metadata Grid Query

**Endpoint:** `GET /metadata/columns`

IMPORTANT: the grid must read from `column_catalog_editor`.

```sql
SELECT *
FROM column_catalog_editor
WHERE workspace_id = ?
  AND (table_name = ? OR ? IS NULL)
LIMIT 200
```

**Endpoint:** `GET /metadata/tables`

Returns the distinct, sorted list of table names present in the caller's workspace projection (`string[]`), used to populate the table filter dropdown in the grid toolbar.

## 12. Pagination Contract

`GET /metadata/columns`

**Query parameters:**
- `limit` (default 200)
- `offset`
- `search`
- `undocumented_only`
- `table_name` — exact match against `column_catalog_editor.table_name`; omit or leave blank for all tables

**Example:**
```
GET /metadata/columns?limit=200&offset=0&table_name=orders
```

## 13. CSV Metadata Upload

**Endpoint:** `POST /metadata/upload` (`multipart/form-data`, file field named `file`)

**CSV format** (snake_case headers):
```
schema_name,table_name,column_name,data_type
sales,orders,id,uuid
sales,orders,status,varchar
```

A CSV upload guide describing this format is served at `/csv-guide`.

**Upload flow:**
```
Upload CSV
  → Parse metadata
  → Upsert schemas
  → Upsert tables
  → Upsert columns
  → Refresh projection table
```

Upserts are workspace-scoped and keyed by name — a schema, table, or column with the same name in the same scope is reused rather than duplicated.

## 14. Projection Refresh Strategy

When metadata changes occur:

```sql
DELETE FROM column_catalog_editor
WHERE workspace_id = ?

INSERT INTO column_catalog_editor
SELECT ...
FROM schemas
JOIN tables
JOIN columns
LEFT JOIN term_column_mapping
LEFT JOIN business_terms
```

`ProjectionService.RefreshAsync` is the single entry point used by all callers (CSV upload, bulk column update, business term mapping, workspace copy, demo seeding).

## 15. Spreadsheet Metadata Editor

**Grid Columns**
- Schema
- Table
- Column
- Example Value
- Owner
- Description
- Business Term

**Toolbar**
- Search box
- "Undocumented only" checkbox
- Table filter dropdown (populated from `GET /metadata/tables`; defaults to the first table on load)
- "Business Terms" button — opens the business terms panel as a slide-over rather than an embedded page section
- "Upload CSV" button

**Features**
- Inline editing
- Keyboard navigation
- Filtering
- Sorting
- Bulk paste
- Optimistic updates
- Row virtualization (handles 100k+ rows without DOM bloat)

## 16. Bulk Metadata Updates

**Endpoint:** `PATCH /metadata/columns`

This endpoint accepts BULK updates.

**Request example:**
```json
[
  {
    "columnId": "...",
    "description": "...",
    "exampleValue": "...",
    "owner": "...",
    "version": 1
  }
]
```

A version mismatch on any item returns `409 Conflict`.

## 17. Business Terms

**Endpoints:**
```
POST /business-terms
GET  /business-terms
POST /business-terms/map
```

`GET /business-terms` returns all terms for the workspace so the frontend can populate dropdown selections.

**Example response:**
```json
[
  {
    "id": "...",
    "name": "...",
    "definition": "..."
  }
]
```

Column-term relationships use `term_column_mapping`. Columns may belong to multiple terms.

## 18. Metadata Audit Log

All metadata edits must write to `metadata_changes`.

**Triggered by:**
```
PATCH /metadata/columns
```

Only fields that actually changed get an audit record — each update diffs `description`, `example_value`, and `owner` individually against the current row, and writes one `metadata_changes` entry per changed field (not one per request or one per column).

## 19. Documentation Coverage

**Endpoint:** `GET /metadata/coverage`

Returns workspace-level coverage.

**Response:**
```json
{
  "totalColumns": 0,
  "documentedColumns": 0,
  "coveragePercent": 0
}
```

Because coverage reads from the projection table (not the source tables), it stays in sync automatically whenever the projection is refreshed.

## 20. Demo Dataset (Seed Data)

Workspace seeded with demo dataset: **Acme Commerce Analytics**. This workspace is itself a **template** (`is_template = true`), and the seeded invite (token `demo`) is a **template invite** (`template_workspace_id` = the demo workspace's own id) — every person who joins via `/invite/demo` gets their own private copy, not a shared workspace.

**Schemas:** `sales`, `marketing`, `product`

**Tables and columns:**

`sales.orders` — id (uuid), customer_id (uuid), status (varchar), total_amount (numeric), created_at (timestamp)

`sales.order_items` — id (uuid), order_id (uuid), product_id (uuid), quantity (int), unit_price (numeric)

`sales.customers` — id (uuid), email (varchar), first_name (varchar), last_name (varchar), created_at (timestamp)

`marketing.campaigns` — id (uuid), name (varchar), channel (varchar), budget (numeric), start_date (date), end_date (date)

`marketing.leads` — id (uuid), email (varchar), source (varchar), status (varchar), created_at (timestamp)

`product.products` — id (uuid), name (varchar), sku (varchar), price (numeric), category_id (uuid)

`product.categories` — id (uuid), name (varchar), parent_id (uuid)

`product.inventory` — id (uuid), product_id (uuid), warehouse (varchar), quantity (int), updated_at (timestamp)

38 columns total. Some columns should start with descriptions populated and others left undocumented so coverage metrics are visible.

### Idempotency

The seeder checks whether the workspace already exists before creating anything:
- **Workspace absent:** creates all data, saves, then refreshes the projection.
- **Workspace present, projection empty:** refreshes the projection (recovery from a failed or interrupted first run).
- **Workspace present, projection populated:** exits immediately.

## 21. Performance Requirements

System must support 100k+ columns.

**Required optimizations:**
- Projection table reads
- Pagination (`LIMIT 200`)
- Indexed filters
- Batched updates
- Optimistic UI updates
- Row versioning
- Frontend row virtualization (`@tanstack/react-virtual`)

## 22. API Endpoints

```
GET  /invite/{token}
POST /invite/{token}/join
POST /invites

POST /metadata/upload
GET  /metadata/columns
GET  /metadata/tables
PATCH /metadata/columns

POST /business-terms
GET  /business-terms
POST /business-terms/map

GET  /metadata/coverage
```

**Development only:**
```
GET  /dev/workspaces
POST /dev/workspaces/{id}/join
```

## 23. Non-Goals (MVP)

Do NOT implement:

- Authentication providers
- Database connectors
- Lineage engines
- Governance workflows
- RBAC
- AI assistants
