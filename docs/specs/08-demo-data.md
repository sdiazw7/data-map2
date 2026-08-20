[← Index](../specs.md)

## 20. Demo Dataset (Seed Data)

Workspace seeded with demo dataset: **Acme Commerce Analytics**. This workspace is itself a **template** (`is_template = true`), and the seeded invite (token `demo`) is a **template invite** (`template_workspace_id` = the demo workspace's own id) — every person who joins via `/invite/demo` gets their own private copy, not a shared workspace.

**Schemas:** `sales`, `marketing`, `product`

**Tables and columns:**

`sales.orders` — id (uuid), customer_id (uuid), status (varchar), total_amount (numeric), created_at (timestamp)

`sales.order_items` — id (uuid), order_id (uuid), product_id (uuid), quantity (int), unit_price (numeric)

`sales.customers` — id (uuid), email (varchar), first_name (varchar), last_name (varchar), created_at (timestamp)

`marketing.campaigns` — id (uuid), name (varchar), channel (varchar), budget (numeric), start_date (date), end_date (date)

`marketing.leads` — id (uuid), email (varchar), source (varchar), status (varchar), created_at (timestamp)

`product.products` — id (uuid), name (varchar), sku (varchar), price (numeric), category_id (uuid)

`product.categories` — id (uuid), name (varchar), parent_id (uuid)

`product.inventory` — id (uuid), product_id (uuid), warehouse (varchar), quantity (int), updated_at (timestamp)

38 columns total. Some columns should start with descriptions populated and others left undocumented so coverage metrics are visible.

### Idempotency

The seeder checks whether the workspace already exists before creating anything:
- **Workspace absent:** creates all data, saves, then refreshes the projection.
- **Workspace present, projection empty:** refreshes the projection (recovery from a failed or interrupted first run).
- **Workspace present, projection populated:** exits immediately.
