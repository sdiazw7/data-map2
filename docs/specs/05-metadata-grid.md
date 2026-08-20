[← Index](../specs.md)

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
