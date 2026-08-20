[← Index](../specs.md)

## 1. Backend Architecture

**Request Flow**

```
Endpoints → Services → Repositories → Database
```

- **Endpoints** handle HTTP requests and validation.
- **Services** contain business logic and orchestration.
- **Repositories** handle database queries only.

`SessionAuthMiddleware` sits in front of every endpoint except `GET /health` and `GET|POST /invite/*`, and populates `HttpContext.Items["ParticipantId"]` / `["WorkspaceId"]` for downstream use.
