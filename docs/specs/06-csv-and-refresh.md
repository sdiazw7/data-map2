[← Index](../specs.md)

## 1. CSV Metadata Upload

**Endpoint:** `POST /metadata/upload` (`multipart/form-data`, file field named `file`)

**CSV format** (snake_case headers):
```
schema_name,table_name,column_name,data_type
sales,orders,id,uuid
sales,orders,status,varchar
```

A CSV upload guide describing this format is served at `/csv-guide`.

**Response (`200 OK`):** a summary of what the import did, so the caller can report it rather
than guess.
```json
{
  "rows": 38,
  "schemas": 3,
  "tables": 8,
  "columnsCreated": 30,
  "columnsUpdated": 8
}
```

**Upload flow:**
```
Upload CSV
  → Validate and parse the file
  → Upsert schemas   ─┐
  → Upsert tables     ├─ one transaction
  → Upsert columns    │
  → Refresh projection table ─┘
```

Upserts are workspace-scoped and keyed by name — a schema, table, or column with the same name in
the same scope is reused rather than duplicated. Name matching is **case-sensitive**, matching the
unique indexes on `(workspace_id, name)`; `Sales` and `sales` are two schemas, not one.

Each level is upserted as a single batch — one read of the existing rows, one write — rather than a
query per row. At 100k+ columns a per-row upsert costs two round trips per line, which the
performance requirement ([§1, Non-Functional](09-non-functional.md#1-performance-requirements))
does not tolerate.

The whole flow commits as one transaction. The projection refresh is a delete-then-reinsert
(see §2), so committing the upserts separately would leave a window where the catalog no longer
matches the projection.

**Validation** — each returns `400 Bad Request`, never a 500:
- an empty file, or one over 25 MB
- a filename that is not `.csv`
- headers that do not match the four expected columns, or a file the CSV parser cannot read
- a file with no data rows
- more than 200,000 rows (split the file)
- any row missing `schema_name`, `table_name`, `column_name`, or `data_type`, or with a value over 200 characters — reported with the offending row number, up to the first 10

Values are trimmed of surrounding whitespace before use. A file with any invalid row is rejected
whole; no partial import occurs.

## 2. Projection Refresh Strategy

The projection has two maintenance paths. Which one applies depends on whether the
change alters the *structure* of the catalog or only the *contents* of existing rows.

### Full rebuild — structural changes only

Used by CSV upload, workspace copy, and demo seeding, where rows are added or removed
wholesale:

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

The delete and the insert MUST run in one transaction. Committed separately, the delete
becomes visible on its own — concurrent readers see an empty catalog mid-rebuild, and a
failed insert leaves the workspace's projection permanently empty.

Entry point: `ProjectionService.RefreshAsync`.

### Targeted sync — edits to existing rows

Used by bulk column update and business term mapping. These change field values on rows
that already exist, so they update those rows in place instead of rebuilding.

A full rebuild here would not scale: with 100k+ columns in a workspace, every
optimistically-saved cell edit would cost a 100k-row delete plus a 100k-row reinsert
across a five-table join. Targeted sync makes the cost proportional to the number of
rows actually edited.

Entry points: `ProjectionService.SyncColumnsAsync` (description, example value, owner,
version) and `ProjectionService.SyncColumnTermAsync` (business term).

Never call `RefreshAsync` from an edit path.
