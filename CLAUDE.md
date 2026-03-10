# CLAUDE.md — Root

## Product Summary

A shared metadata catalog for analytics teams. Users enter via invite link (no auth providers — don't suggest adding them). Core UI is a spreadsheet-style grid for documenting DB columns with keyboard navigation and bulk paste. Must handle 100k+ columns via paginated queries and a dedicated read table. Edits are saved optimistically and logged for audit.

## Architecture

```
Frontend (React SPA)
    ↓ REST API (DTOs)
Backend (ASP.NET Core)
    ↓
PostgreSQL
```

**Hard rules:**
- Frontend never accesses the database directly
- All data flows through the backend REST API
- Endpoints are thin (routing + request validation only)
- Services own all business logic
- Repositories own all data access
- DTOs are used at the API boundary — never expose domain models directly

## Layered Responsibilities

| Layer      | Responsibility                        |
|------------|---------------------------------------|
| Endpoint   | Route, validate request, call service |
| Service    | Validation, authorization, domain logic — inherits `BaseService` |
| Repository | Persistence only — no business logic  |
| DTO        | API request/response shapes           |

## API Design

- RESTful resource naming, plural nouns (`/tables`, `/columns`)
- All list endpoints support pagination
- Consistent structured error responses
- HTTP status codes used correctly

## Logging

Use structured logging with named placeholders. Never interpolate or concatenate.

**Always include:** request ID, org ID, user ID, operation result

**Never log:** secrets, credentials, connection strings

## General Coding Standards

- Prefer simple, readable solutions over clever ones
- Keep functions small and single-purpose
- Avoid premature abstraction
- No business logic in controllers or UI components

## Never Do

- Never put business logic in endpoints or UI components
- Never expose domain models directly via API — always map to DTOs
- Never commit secrets, credentials, or connection strings
- Never use string concatenation to build SQL queries
- Never use `any` in TypeScript
- Never call `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` on async code
- Never auto-migrate the database on application startup in production
- Never log sensitive data (secrets, tokens, PII, connection strings)
- Never manually instantiate services or repositories — use DI
- Never access the database from the frontend
