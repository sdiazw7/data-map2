# Feature Flows

Developer reference for how each feature works end-to-end. Covers the full stack: frontend components and hooks → API endpoints → services → repositories → database.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Session & Authentication](#session--authentication)
3. [Creating an Invite Link](#creating-an-invite-link)
4. [Joining via Invite Link](#joining-via-invite-link)
5. [Workspace & Metadata Grid](#workspace--metadata-grid)
6. [Editing Columns](#editing-columns)
7. [CSV Upload](#csv-upload)
8. [Business Terms](#business-terms)
9. [Coverage Tracking](#coverage-tracking)
10. [Demo Data Seeding](#demo-data-seeding)
11. [The Projection Table](#the-projection-table)

---

## Architecture Overview

```
Browser (React SPA, port 5173)
    ↓ REST API (JSON over HTTP)
ASP.NET Core (port 5000)
    ↓
PostgreSQL (app schema)
```

**Layering rules:**
- Endpoints are thin: route, validate, call a service, return the result.
- Services own all business logic and validation.
- Repositories own all data access — no business logic.
- DTOs are used at the API boundary; domain models never leave the backend.

**Frontend layering:**
- Pages (`components/pages/`) render layout and compose feature components.
- Hooks (`hooks/`) encapsulate state and call services.
- Services (`services/`) make raw `fetch` calls — no UI logic.

---

## Session & Authentication

There is no login system. Access is granted via an invite link. After joining, the session is maintained using a combination of a **server-side cookie** and **localStorage**.

### How it works

After a user joins (see below), the backend:
1. Creates a `ParticipantSession` record in the database.
2. Sets an `HttpOnly` cookie named `participant_session` containing the session ID (a UUID).
3. Returns a `JoinResponse` JSON body containing `participantId`, `workspaceId`, `workspaceName`, and `email`.

The frontend saves the `JoinResponse` to `localStorage` (key: `datamap_session`) via `useSession`. The cookie is used for all subsequent authenticated API calls (`credentials: 'include'` is set on every `fetch`).

### Request authentication (`SessionAuthMiddleware`)

Every request (except `GET /health` and `GET|POST /invite/*`) passes through `SessionAuthMiddleware`:

1. Reads the `participant_session` cookie.
2. Looks up the `ParticipantSession` by ID in the database.
3. Rejects with `401` if the cookie is missing, invalid, or the session is older than 30 days.
4. Sets `context.Items["ParticipantId"]` and `context.Items["WorkspaceId"]` for use by the endpoint.
5. Updates `LastSeenAt` on the session.

All protected endpoints extract `WorkspaceId` from `context.Items` to scope queries to the user's workspace.

**Key files:**
- `DataMap.Api/Middleware/SessionAuthMiddleware.cs`
- `DataMap.Api/Repositories/SessionRepository.cs`
- `frontend/src/hooks/useSession.ts`
- `frontend/src/utils/api.ts`

---

## Creating an Invite Link

Invite creation is a protected operation — the caller must have an active session (i.e. already be a participant in a workspace).

### Endpoint

```
POST /invites
```

The workspace is taken from the caller's session; it does not need to be in the request body.

### Request body

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
| `templateWorkspaceId` | No | If set, each new user gets their own copy of this workspace instead of joining a shared one. The referenced workspace must exist and have `IsTemplate = true`. |

### Response (`201 Created`)

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

The `token` is a 32-byte cryptographically random value encoded as base64url. Share it as `/invite/{token}`.

### Backend flow (`InviteService.CreateAsync`)

1. Validates `MaxUses >= 1` and `ExpiresAt` is in the future.
2. If `TemplateWorkspaceId` is provided, fetches the workspace and verifies `IsTemplate = true`. Throws `TemplateWorkspaceNotFoundException` (→ `404`) if not found or not a template.
3. Generates a URL-safe token: `RandomNumberGenerator.GetBytes(32)` encoded as base64url.
4. Persists the `Invite` record and returns `CreateInviteResponse`.

### Two invite modes

| Mode | `templateWorkspaceId` | Join behaviour |
|---|---|---|
| Shared | `null` | All users join the same workspace. |
| Template | set to a template workspace ID | Each new user receives their own copy of the template workspace. |

**Key files:**
- `DataMap.Api/Endpoints/InviteEndpoints.cs`
- `DataMap.Api/Services/InviteService.cs` — `CreateAsync`
- `DataMap.Api/Repositories/InviteRepository.cs` — `CreateAsync`
- `DataMap.Api/Repositories/WorkspaceRepository.cs` — `GetByIdAsync`
- `DataMap.Api/DTOs/CreateInviteRequest.cs`
- `DataMap.Api/DTOs/CreateInviteResponse.cs`

---

## Joining via Invite Link

### URL shape

```
/invite/:token
```

The demo workspace uses token `demo`: `http://localhost:5173/invite/demo`.

### Frontend flow

1. `InvitePage` reads the `:token` from the URL via React Router.
2. `useInvite` calls `GET /invite/:token` on mount to load invite metadata (workspace name, validity).
3. If the invite is invalid or not found, an appropriate message is shown.
4. User enters their email and submits the form.
5. `joinInvite` calls `POST /invite/:token/join` with `{ email }`.
6. On success: session is saved to localStorage, user is navigated to `/workspace`.

### Backend flow (`POST /invite/:token/join`)

Handled by `InviteService.JoinAsync`:

1. Fetches the invite by token (including the related Workspace).
2. Validates: invite must not be expired, must not have exceeded `MaxUses`.
3. Looks for an existing `Participant` with the same email in the workspace.
   - **Returning user:** updates `LastSeenAt`, does not increment `UsedCount`.
   - **New user:** creates a `Participant` record, increments `UsedCount`.
4. Creates a `ParticipantSession` with `WorkspaceId` from the invite.
5. Sets the `participant_session` cookie (`HttpOnly`, `SameSite=Lax`, 30-day expiry).
6. Returns `JoinResponse`.

**Key files:**
- `DataMap.Api/Endpoints/InviteEndpoints.cs`
- `DataMap.Api/Services/InviteService.cs`
- `DataMap.Api/Repositories/InviteRepository.cs`
- `DataMap.Api/Repositories/ParticipantRepository.cs`
- `frontend/src/components/pages/InvitePage.tsx`
- `frontend/src/hooks/useInvite.ts`
- `frontend/src/services/inviteService.ts`

---

## Workspace & Metadata Grid

The workspace page is the main UI. It shows a virtualized spreadsheet grid of all columns in the workspace.

### Frontend flow

`WorkspacePage` mounts and:
1. Reads the session from localStorage via `useSession`. If absent, shows "No active session" message.
2. `useMetadataColumns` calls `GET /metadata/columns` with optional `search` and `undocumented_only` query params.
3. `useCoverage` calls `GET /metadata/coverage` for the progress banner.
4. `useBusinessTerms` calls `GET /business-terms` to populate the term dropdown in each row.
5. `MetadataGrid` renders the result using TanStack Table with `@tanstack/react-virtual` for virtual scrolling (handles 100k+ rows without DOM bloat).

### Backend flow (`GET /metadata/columns`)

1. Middleware extracts `WorkspaceId` from the session cookie.
2. `MetadataService.GetColumnsAsync` calls `ProjectionRepository.QueryAsync`.
3. The projection repository queries the `column_catalog_editor` read table — a denormalized flat table that joins columns, tables, schemas, and business terms.
4. Supports `search` (ILIKE across schema, table, column name), `undocumented_only` (no description), `limit`, and `offset` for pagination.
5. Returns a list of `ColumnGridRow` DTOs.

**Key files:**
- `DataMap.Api/Endpoints/MetadataEndpoints.cs`
- `DataMap.Api/Services/MetadataService.cs`
- `DataMap.Api/Repositories/ProjectionRepository.cs`
- `frontend/src/components/pages/WorkspacePage.tsx`
- `frontend/src/components/grid/MetadataGrid.tsx`
- `frontend/src/hooks/useMetadataColumns.ts`

---

## Editing Columns

Users can edit cells directly in the grid (description, example value, owner). Changes are saved optimistically — the grid does not block on the save.

### Frontend flow

1. User edits a cell in `GridCell` and blurs or presses Enter.
2. `onUpdate` is called with a `ColumnUpdateRequest` containing the column ID, all three editable fields, and the current `version`.
3. `WorkspacePage.handleUpdate` calls `useBulkUpdate.mutate([update])`.
4. `useBulkUpdate` calls `PATCH /metadata/columns` with the array of updates.

### Backend flow (`PATCH /metadata/columns`)

Handled by `MetadataService.BulkUpdateAsync`:

1. For each update, fetches the `Column` record by ID (scoped to the workspace).
2. **Optimistic concurrency check:** if `column.Version` does not match `update.Version`, throws `VersionConflictException` (→ `409 Conflict`). This prevents a stale client from overwriting a more recent edit.
3. Diffs each editable field (description, exampleValue, owner). For each changed field, creates a `MetadataChange` audit record.
4. Increments `column.Version` and updates `UpdatedAt`.
5. Persists the updated column and all change records.
6. Calls `ProjectionService.RefreshAsync` to update the read table.

**Key files:**
- `DataMap.Api/Services/MetadataService.cs` — `BulkUpdateAsync`
- `DataMap.Api/Repositories/ColumnRepository.cs`
- `DataMap.Api/Repositories/MetadataChangeRepository.cs`
- `frontend/src/components/grid/GridCell.tsx`
- `frontend/src/hooks/useBulkUpdate.ts`

---

## CSV Upload

Users can upload a CSV file to bulk-import columns into their workspace.

### CSV format

```
schema_name,table_name,column_name,data_type
sales,orders,id,uuid
sales,orders,status,varchar
```

A CSV upload guide is available at `/csv-guide`.

### Frontend flow

1. User clicks "Upload CSV" in `GridToolbar`, which opens `CsvUploadModal`.
2. User selects a file and submits.
3. The modal calls `POST /metadata/upload` with `multipart/form-data`, file field named `file`.
4. On success, `WorkspacePage` calls `reloadColumns()` and `reloadCoverage()` to refresh the grid.

### Backend flow (`POST /metadata/upload`)

Handled by `MetadataService.UploadCsvAsync`:

1. Parses the CSV using CsvHelper, mapping snake_case headers to `CsvColumnRecord`.
2. Iterates each row. For each unique schema name, upserts a `Schema` record (create or return existing). For each unique (schema, table) pair, upserts a `Table`. For each column row, upserts a `Column`.
3. All upserts are workspace-scoped. Existing records are not duplicated.
4. After all rows are processed, calls `ProjectionService.RefreshAsync` to rebuild the read table.

**Key files:**
- `DataMap.Api/Endpoints/MetadataEndpoints.cs` — `POST /metadata/upload`
- `DataMap.Api/Services/MetadataService.cs` — `UploadCsvAsync`
- `DataMap.Api/Repositories/SchemaRepository.cs`, `TableRepository.cs`, `ColumnRepository.cs`
- `frontend/src/components/upload/CsvUploadModal.tsx`
- `frontend/src/components/pages/CsvUploadGuidePage.tsx`

---

## Business Terms

Business terms are workspace-level labels (e.g. "Monthly Recurring Revenue") that can be mapped to one or more columns to create a shared vocabulary.

### Creating a term

- Frontend: a form in the workspace calls `POST /business-terms` with `{ name, definition }`.
- Backend (`BusinessTermService.CreateAsync`): validates the name is non-empty, creates a `BusinessTerm` record scoped to the workspace.

### Mapping a term to a column

- Frontend: the "Business Term" column in the grid shows a dropdown of all workspace terms (`useBusinessTerms`). Selecting one calls `WorkspacePage.handleTermMap`.
- `handleTermMap` calls `mapTermToColumn` which calls `POST /business-terms/map` with `{ termId, columnId }`.
- Backend (`BusinessTermService.MapToColumnAsync`):
  1. Verifies the term belongs to the workspace.
  2. Creates a `TermColumnMapping` join record.
  3. Calls `ProjectionService.RefreshAsync` so the business term appears in the grid immediately.

The grid's `business_term` column in the projection is populated via a `LEFT JOIN` on `term_column_mappings` and `business_terms`.

**Key files:**
- `DataMap.Api/Endpoints/BusinessTermEndpoints.cs`
- `DataMap.Api/Services/BusinessTermService.cs`
- `DataMap.Api/Repositories/BusinessTermRepository.cs`
- `frontend/src/components/grid/BusinessTermCell.tsx`
- `frontend/src/hooks/useBusinessTerms.ts`
- `frontend/src/services/businessTermService.ts`

---

## Coverage Tracking

The coverage banner at the top of the workspace shows how many columns have a description filled in.

### Frontend flow

`useCoverage` calls `GET /metadata/coverage` on mount and after every edit or upload (via `reloadCoverage()`).

The response is a `CoverageResponse`:
```json
{ "total": 38, "documented": 7, "percent": 18.4 }
```

`CoverageBanner` renders this as a progress bar and text (e.g. "7/38 columns documented, 18.4%").

### Backend flow (`GET /metadata/coverage`)

`MetadataService.GetCoverageAsync` calls `ProjectionRepository.GetCoverageCountsAsync`:
- `total`: count of all rows in `column_catalog_editor` for the workspace.
- `documented`: count of rows where `description` is non-null and non-empty.
- `percent`: rounded to one decimal place.

Because coverage reads from the projection table (not the source tables), it stays in sync automatically whenever the projection is refreshed.

**Key files:**
- `DataMap.Api/Services/MetadataService.cs` — `GetCoverageAsync`
- `DataMap.Api/Repositories/ProjectionRepository.cs` — `GetCoverageCountsAsync`
- `frontend/src/hooks/useCoverage.ts`
- `frontend/src/components/coverage/CoverageBanner.tsx`

---

## Demo Data Seeding

On every startup, `DemoDataSeeder.SeedAsync` runs before the HTTP server accepts requests.

### What it creates

- 1 workspace: **Acme Commerce Analytics**
- 1 invite with token `demo` (valid for 1 year, max 100 uses)
- 3 schemas: `sales`, `marketing`, `product`
- 8 tables: `orders`, `order_items`, `customers`, `campaigns`, `leads`, `products`, `categories`, `inventory`
- 38 columns across those tables

### Idempotency

The seeder checks whether the workspace already exists before creating anything:
- **Workspace absent:** creates all data, saves, then calls `RefreshAsync` to populate the projection.
- **Workspace present, projection empty:** calls `RefreshAsync` to repopulate the projection (recovery from a failed or interrupted first run).
- **Workspace present, projection populated:** exits immediately.

**Key file:**
- `DataMap.Api/Seed/DemoDataSeeder.cs`

---

## The Projection Table

`column_catalog_editor` is a denormalized read table. It exists to make the grid query fast and simple — a single `SELECT` with filtering and pagination, instead of joining four tables on every page load.

### Schema

| Column | Source |
|---|---|
| `workspace_id` | `columns.workspace_id` |
| `column_id` | `columns.id` |
| `schema_name` | `schemas.name` |
| `table_name` | `tables.name` |
| `column_name` | `columns.name` |
| `data_type` | `columns.data_type` |
| `example_value` | `columns.example_value` |
| `description` | `columns.description` |
| `business_term` | `business_terms.name` (nullable) |
| `owner` | `columns.owner` |
| `version` | `columns.version` |

### When it is refreshed

The projection is rebuilt (delete + re-insert for the workspace) after every operation that changes the underlying data:

| Operation | Triggered by |
|---|---|
| CSV upload | `MetadataService.UploadCsvAsync` |
| Column edit (bulk update) | `MetadataService.BulkUpdateAsync` |
| Business term mapped | `BusinessTermService.MapToColumnAsync` |
| App startup (if empty) | `DemoDataSeeder.SeedAsync` |

`ProjectionService.RefreshAsync` is the single entry point used by all callers. It delegates to `ProjectionRepository.RefreshAsync`, which runs a `DELETE` via EF Core followed by a raw `INSERT ... SELECT` SQL statement joining the source tables.

**Key files:**
- `DataMap.Api/Repositories/ProjectionRepository.cs`
- `DataMap.Api/Services/ProjectionService.cs`
- `DataMap.Api/Models/ColumnCatalogEditor.cs`
