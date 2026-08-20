[← Index](../specs.md)

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
