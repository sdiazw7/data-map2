[← Index](../specs.md)

## 1. Metadata Grid Query

**Endpoint:** `GET /metadata/columns`

IMPORTANT: the grid must read from `column_catalog_editor`.

```sql
SELECT *
FROM column_catalog_editor
WHERE workspace_id = ?
  AND (table_name = ? OR ? IS NULL)
ORDER BY <sort_column> ASC|DESC
LIMIT 200
```

**Endpoint:** `GET /metadata/tables`

Returns the distinct, sorted list of table names present in the caller's workspace projection (`string[]`), used to populate the table filter dropdown in the grid toolbar.

## 2. Pagination Contract

`GET /metadata/columns`

**Query parameters:**
- `limit` (default 200, minimum 1, maximum 1000) — outside that range returns `400 Bad Request`
- `offset` (default 0) — negative values return `400 Bad Request`
- `search`
- `undocumented_only`
- `table_name` — exact match against `column_catalog_editor.table_name`; omit or leave blank for all tables
- `sort_by` — one of `column_name`, `table_name`, `data_type`, `owner` (default `column_name`); no other field is accepted
- `sort_dir` — `asc` (default) or `desc`

`description` is deliberately not a sortable field — it's free text with no meaningful order, and sorting it would be the most expensive column to order on for the least benefit. It remains available as a `search` target only. A query with `sort_by=description` returns `400 Bad Request`.

`limit` is capped rather than left open because a workspace holds 100k+ columns: an unbounded
limit reads the whole catalog into memory and defeats the point of paginating. The endpoint's
default is only a default — the bound is enforced in the service, so a hand-written query string
cannot exceed it.

Results must always be returned with a deterministic `ORDER BY` (never an unordered `Skip`/`Take`) — without one, offset pagination can return duplicate or missing rows across pages.

**Example:**
```
GET /metadata/columns?limit=200&offset=0&table_name=orders&sort_by=table_name&sort_dir=desc
```

## 3. Spreadsheet Metadata Editor

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
- Column sorting on Table, Column, Data Type, Owner — click a header to sort (see §2 for the `sort_by`/`sort_dir` contract; Schema, Description, and Business Term are not sortable)
- Bulk paste
- Optimistic updates
- Row virtualization (handles 100k+ rows without DOM bloat)

## 4. Bulk Metadata Updates

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

**The batch is all-or-nothing.** Every item is read and version-checked before anything is
written, and the column writes, audit records ([§2, Business Terms and Audit](07-business-terms-and-audit.md#2-metadata-audit-log))
and projection sync then commit in a single transaction. A version mismatch on any item returns
`409 Conflict` and writes nothing at all.

Partial application is not acceptable here even though the request is a batch. Committing the
earlier items and then failing would persist those edits while dropping their audit records, and
would leave the projection holding their old values *and their old version* — so the grid, which
reads from the projection, would keep sending a stale version and every later edit to those cells
would 409 indefinitely.

**Request validation** — each returns `400 Bad Request`:
- an empty array
- more than 5000 items in one request
- the same `columnId` more than once in one request (it would bump `version` twice and produce an audit trail that reads as though the first edit never happened)
- `description` over 4000, `exampleValue` over 1000, or `owner` over 200 characters
