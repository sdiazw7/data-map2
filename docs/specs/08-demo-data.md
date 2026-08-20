[← Index](../specs.md)

## 1. Demo Dataset (Seed Data)

Workspace seeded with demo dataset: **Acme Commerce Analytics**. This workspace is itself a **template** (`is_template = true`), and the seeded invite (token `demo`) is a **template invite** (`template_workspace_id` = the demo workspace's own id) — every person who joins via `/invite/demo` gets their own private copy, not a shared workspace.

**Schemas:** `sales`, `marketing`, `product` — 8 tables, ~38 columns total, spanning typical e-commerce entities (orders, customers, campaigns, products, inventory, etc.). Some columns start with descriptions populated and others left undocumented so coverage metrics are visible.

The exact tables and columns are defined in `DemoDataSeeder.cs` — not duplicated here to avoid the spec drifting out of sync as the seed data evolves.

### Idempotency

The seeder checks whether the workspace already exists before creating anything:
- **Workspace absent:** creates all data, saves, then refreshes the projection.
- **Workspace present, projection empty:** refreshes the projection (recovery from a failed or interrupted first run).
- **Workspace present, projection populated:** exits immediately.
