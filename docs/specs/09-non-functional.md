[← Index](../specs.md)

## 1. Performance Requirements

System must support 100k+ columns. This drives several design decisions specced elsewhere — the projection table ([§2, Data Model](04-data-model.md#2-projection-table-read-model)), pagination and row virtualization ([§2–3, Metadata Grid](05-metadata-grid.md#2-pagination-contract)), and optimistic updates with row versioning ([§4, Metadata Grid](05-metadata-grid.md#4-bulk-metadata-updates)) — rather than being a separate list of mechanisms to keep in sync here.

## 2. Non-Goals (MVP)

Do NOT implement:

- Authentication providers
- Database connectors
- Lineage engines
- Governance workflows
- RBAC
- AI assistants
