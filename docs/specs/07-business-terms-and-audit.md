[← Index](../specs.md)

## 1. Business Terms

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

## 2. Metadata Audit Log

All metadata edits must write to `metadata_changes`.

**Triggered by:**
```
PATCH /metadata/columns
```

The audit records are written inside the same transaction as the column edits they describe
([§4, Metadata Grid](05-metadata-grid.md#4-bulk-metadata-updates)). "All metadata edits" is only
true if the two commit together — committed separately, a failure in between persists edits that
the audit log has no record of.

Only fields that actually changed get an audit record — each update diffs `description`, `example_value`, and `owner` individually against the current row, and writes one `metadata_changes` entry per changed field (not one per request or one per column).

## 3. Documentation Coverage

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
