[← Index](../specs.md)

## 1. Backend Architecture

**Request Flow**

```
Endpoints → Services → Repositories → Database
```

- **Endpoints** handle HTTP requests and validation.
- **Services** contain business logic and orchestration.
- **Repositories** handle database queries only.

Each repository call commits its own `SaveChangesAsync`. A service whose operation spans several
repositories therefore has to declare its own transaction boundary, or a failure partway through
leaves the write half-applied — `IUnitOfWork.ExecuteAsync` wraps such an operation so it commits or
rolls back as a unit. It is re-entrant: a nested call joins the ambient transaction rather than
opening a second one. Single-repository operations do not need it.

`SessionAuthMiddleware` sits in front of every endpoint except `GET /health` and `GET|POST /invite/*`, and populates `HttpContext.Items["ParticipantId"]` / `["WorkspaceId"]` for downstream use.
