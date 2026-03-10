# CLAUDE.md — Backend

> Architecture rules are defined in the root `CLAUDE.md`. This file covers backend-specific implementation details.

## Dependency Injection

- All services and repositories are injected via constructor — never manually instantiated
- Prefer interfaces for service and repository dependencies (`ITableService`, `ITableRepository`)
- Register everything in the DI container at startup

## Endpoint Rules

<!-- Code example included because "thin" is easy to misjudge without a concrete pattern to follow -->

Endpoints must stay thin — define the route, validate the request, call a service and return the result:
```csharp
app.MapPost("/tables", async (CreateTableRequest req, ITableService svc) =>
{
    var result = await svc.CreateAsync(req);
    return Results.Created($"/tables/{result.Id}", result);
});
```

## Service Pattern

All services inherit `BaseService`.

Responsibilities:
- Input validation
- Authorization checks
- Domain logic
- Orchestrating repository calls

## Repository Pattern

Repositories are for persistence only — no business logic, no domain rules.

## Database Access
- Always use EF Core or parameterized queries
- Never construct SQL via string concatenation or interpolation

## Required Packages

When scaffolding the backend, always include:
- `Microsoft.EntityFrameworkCore.Tools` — required for `dotnet ef` migrations

## Local Configuration
- Connection strings go in `appsettings.Local.json` (gitignored, never committed)
- `appsettings.Development.json` log level: `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"`

## Async

Use `async`/`await` for all I/O: database, HTTP, and file operations.

- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`
- Return `Task<T>` or `ValueTask<T>` — avoid `async void`

## Error Handling

Return structured error responses. Use a consistent error envelope:

```json
{
  "error": {
    "code": "TABLE_NOT_FOUND",
    "message": "No table with id 42 exists."
  }
}
```

Map domain exceptions to appropriate HTTP status codes in a central location (middleware or result handler), not inline in endpoints.

## Testing

**Testing stack:**
- xUnit
- FluentAssertions
- Moq
- Integration tests with Testcontainers

**Unit tests** cover services and domain logic in isolation.
- Mock all repository dependencies
- Do not spin up a database or HTTP server
- Name tests: `MethodName_Scenario_ExpectedResult`

**Integration tests** cover endpoints end-to-end.
- Use `WebApplicationFactory` with a real test database
- Each test gets a clean schema — never share state between tests
- Test the HTTP contract: status codes, response shapes, error envelopes

**Repositories are not unit tested directly.**
They are covered by integration tests via the endpoint layer.

**What not to test:**
- EF Core internals
- Mapping/DTO logic unless complex
- Framework behavior
