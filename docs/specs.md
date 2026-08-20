# Build Specification — Spreadsheet-First Metadata Catalog

Index of the build spec, split by area. Each file numbers its own sections starting at §1 — numbers are only unique within a file, so cross-file references are written as "§N, File Name".

| File | Covers |
|---|---|
| [specs/01-overview.md](specs/01-overview.md) | Product goal, tech stack, architecture principles |
| [specs/02-backend-architecture.md](specs/02-backend-architecture.md) | Endpoint → service → repository request flow |
| [specs/03-auth-and-invites.md](specs/03-auth-and-invites.md) | Session auth, invite access flow, dev access, participant upsert, invite creation/validation |
| [specs/04-data-model.md](specs/04-data-model.md) | Database schema, projection table (read model) |
| [specs/05-metadata-grid.md](specs/05-metadata-grid.md) | Grid query, pagination, spreadsheet editor, bulk updates |
| [specs/06-csv-and-refresh.md](specs/06-csv-and-refresh.md) | CSV metadata upload, projection refresh strategy |
| [specs/07-business-terms-and-audit.md](specs/07-business-terms-and-audit.md) | Business terms, audit log, documentation coverage |
| [specs/08-demo-data.md](specs/08-demo-data.md) | Demo dataset (seed data) and idempotency |
| [specs/09-non-functional.md](specs/09-non-functional.md) | Performance requirements, non-goals |

Each endpoint is specified once, in the file for the feature that owns it (invites in §03, grid/CSV in §05–06, business terms in §07) — there's no separate API reference to keep in sync.
