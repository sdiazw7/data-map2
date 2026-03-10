# CLAUDE.project.md — Backend (Project-Specific)

## Stack

- **Runtime:** .NET 10
- **Framework:** ASP.NET Core Minimal APIs
- **ORM:** EF Core 10
- **Driver:** Npgsql
- **Database:** PostgreSQL

## Database

Database name: `datamap`
Schema: `app`

The connection string **must** include `Search Path=app`. Raw SQL in `ProjectionRepository` uses unqualified table names that rely on the search path to resolve to the `app` schema. Without it, those queries silently fail at runtime.
