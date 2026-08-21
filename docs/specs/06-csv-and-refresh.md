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
