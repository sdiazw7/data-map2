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
