# Spreadsheet-First Metadata Catalog

An invite-only web app for analytics teams to document their data. Users access it via an invite link, enter their email, and land in a shared workspace.

Once inside, they can explore a preloaded demo dataset (Acme Commerce Analytics) or upload their own metadata via CSV. The core of the app is a spreadsheet-style grid where they can document database columns — filling in descriptions, owners, example values, and business terms directly in the cells, with keyboard navigation and bulk paste support.

As columns get documented, a coverage tracker shows progress (e.g. 18/64 columns documented, 28%). Business terms can be defined and mapped to multiple columns to create a shared vocabulary across the team.

The system is built to handle 100k+ columns without slowing down, using a dedicated read table for the grid and paginated queries. All edits are logged for audit purposes, and changes are saved optimistically so the UI feels instant.

No logins, no auth providers — just an invite link to get in.

## Stack

- **Frontend:** React 19, TypeScript, Vite, Tailwind CSS, TanStack Table
- **Backend:** ASP.NET Core, Minimal APIs
- **Database:** PostgreSQL
