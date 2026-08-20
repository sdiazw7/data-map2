# Build Specification — Spreadsheet-First Metadata Catalog

Index of the build spec, split by area. Section numbers (`§`) are preserved across files so existing references still resolve.

| File | Sections | Covers |
|---|---|---|
| [specs/01-overview.md](specs/01-overview.md) | §1–3 | Product goal, tech stack, architecture principles |
| [specs/02-backend-architecture.md](specs/02-backend-architecture.md) | §4 | Endpoint → service → repository request flow |
| [specs/03-auth-and-invites.md](specs/03-auth-and-invites.md) | §5–8 | Session auth, invite access flow, dev access, participant upsert, invite creation/validation |
| [specs/04-data-model.md](specs/04-data-model.md) | §9–10 | Database schema, projection table (read model) |
| [specs/05-metadata-grid.md](specs/05-metadata-grid.md) | §11–12, §15–16 | Grid query, pagination, spreadsheet editor, bulk updates |
| [specs/06-csv-and-refresh.md](specs/06-csv-and-refresh.md) | §13–14 | CSV metadata upload, projection refresh strategy |
| [specs/07-business-terms-and-audit.md](specs/07-business-terms-and-audit.md) | §17–19 | Business terms, audit log, documentation coverage |
| [specs/08-demo-data.md](specs/08-demo-data.md) | §20 | Demo dataset (seed data) and idempotency |
| [specs/09-api-reference.md](specs/09-api-reference.md) | §22 | Full API endpoint list |
| [specs/10-non-functional.md](specs/10-non-functional.md) | §21, §23 | Performance requirements, non-goals |
