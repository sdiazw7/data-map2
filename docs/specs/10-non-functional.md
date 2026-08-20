[← Index](../specs.md)

## 21. Performance Requirements

System must support 100k+ columns.

**Required optimizations:**
- Projection table reads
- Pagination (`LIMIT 200`)
- Indexed filters
- Batched updates
- Optimistic UI updates
- Row versioning
- Frontend row virtualization (`@tanstack/react-virtual`)

## 23. Non-Goals (MVP)

Do NOT implement:

- Authentication providers
- Database connectors
- Lineage engines
- Governance workflows
- RBAC
- AI assistants
