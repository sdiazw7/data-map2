# Build Specification — Spreadsheet-First Metadata Catalog

[← Index](../specs.md)

## 1. Product Goal

Build a lightweight metadata catalog that lets invited users:

- Open a workspace via invite link
- Explore a demo dataset
- Upload metadata via CSV
- Document tables and columns in a spreadsheet-style editor
- Define business terms
- Track documentation coverage

The system must scale to 100k+ metadata rows.

MVP constraints:

- No authentication providers
- No external database connectors
- Access via invite links only (dev-only convenience login is the sole exception — see [§2a, Auth & Invites](03-auth-and-invites.md#2a-dev-access-development-only))

## 2. Technology Stack

**Frontend**
- React
- TypeScript
- Vite
- Tailwind CSS
- TanStack Table
- `@tanstack/react-virtual` — row virtualization for the metadata grid, so 100k+ rows don't bloat the DOM

**Backend**
- ASP.NET Core
- Minimal APIs

**Database**
- PostgreSQL

Exact versions are pinned in `frontend/package.json` and `DataMap.Api/DataMap.Api.csproj` — not duplicated here to avoid the spec drifting out of sync as dependencies are upgraded.

## 3. Architecture Principles

- Use explicit relational tables
- Avoid generic asset graphs
- Use UUID primary keys
- Add foreign keys and indexes
- Avoid JSON metadata blobs
- Separate read models and write models
- Optimize for fast metadata grid queries
