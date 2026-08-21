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

### One term per column

Column-term relationships use `term_column_mapping`. **A column carries exactly one business
term, or none.** `term_column_mapping.column_id` is uniquely indexed, and mapping a term onto a
column that already has one *replaces* the existing mapping rather than adding a second.

Three things depend on this:

- **The projection stays flat.** `column_catalog_editor` is keyed by `column_id` and holds a
  single `business_term` field ([§2, Data Model](04-data-model.md#2-projection-table-read-model)).
  A second mapping would fan the rebuild join into two rows sharing that key. Supporting many
  would mean a read-time join, an array column, or a delimited string — the first defeats the
  projection, the second breaks the sort indexes, the third breaks both.
- **The grid is cell-shaped.** Business Term is a column in a spreadsheet with keyboard
  navigation, bulk paste, sorting, and filtering ([§3, Metadata Grid](05-metadata-grid.md#3-spreadsheet-metadata-editor)).
  A multi-valued cell has no coherent answer for what a paste means or which value it sorts on.
- **The term means something.** A column that can carry five terms accumulates five
  loosely-related ones. The constraint is what makes an assignment informative.

**If a column seems to need two terms, add an axis — do not fan this one out.** The pressure
almost always comes from wanting to record two *different kinds* of thing: a sensitivity
classification (`PII`) alongside a meaning (`Customer Email`). The answer is a second
single-valued field next to `business_term`, which keeps every field cell-shaped and the
projection flat. Larger catalogs (DataHub, Collibra) reach the same place — separate typed axes
for terms, tags, and domains — rather than one multi-valued field.

A practical signal to watch for: glossary entries named like `"Revenue / Order Total"`. That is
someone working around this rule, and it means the second axis is now worth adding.

**Validation and errors:**

| Case | Status |
|---|---|
| `name` missing or blank | `400` |
| `name` over 200 characters, or `definition` over 4000 | `400` |
| `name` already used in this workspace | `409` (`BUSINESS_TERM_ALREADY_EXISTS`) |
| mapping references a term outside the caller's workspace, or no term at all | `404` (`BUSINESS_TERM_NOT_FOUND`) |
| mapping references a column outside the caller's workspace, or no column at all | `404` (`COLUMN_NOT_FOUND`) |

`name` is uniquely indexed per workspace, so the duplicate check exists to turn a retyped term
into a `409` the UI can explain instead of a database error surfacing as a `500`. Names are
trimmed before both the check and the insert.

A mapping write and the projection row it feeds commit together, or the grid would show a term
the catalog does not have.

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
