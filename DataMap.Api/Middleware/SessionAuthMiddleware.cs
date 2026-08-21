using DataMap.Api.Repositories;
using Microsoft.AspNetCore.Authorization;

namespace DataMap.Api.Middleware;

public class SessionAuthMiddleware(RequestDelegate next)
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    public async Task InvokeAsync(HttpContext context, ISessionRepository sessionRepo)
    {
        // Public routes opt out individually via AllowAnonymous. This replaced a path-prefix
        // allowlist, under which "/invite" covered the two public invite routes only because
        // /invites happened to be a different segment — renaming one for consistency would
        // have exposed invite creation. Metadata travels with the route, so it cannot drift.
        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(context);
            return;
        }

        // An unmatched path has no endpoint and so no opt-out. Let it through to the routing
        // middleware, which answers 404 — reporting 401 for a route that does not exist would
        // turn the API's shape into something an unauthenticated caller could probe.
        if (endpoint is null)
        {
            await next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue(Endpoints.SessionCookie.Name, out var cookieValue)
            || string.IsNullOrWhiteSpace(cookieValue)
            || !Guid.TryParse(cookieValue, out var sessionId))
        {
            await Unauthorized(context, "UNAUTHORIZED", "Authentication required.");
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            await Unauthorized(context, "UNAUTHORIZED", "Authentication required.");
            return;
        }

        if (session.LastSeenAt < DateTime.UtcNow.Subtract(SessionLifetime))
        {
            await Unauthorized(context, "SESSION_EXPIRED", "Your session has expired. Please sign in again.");
            return;
        }

        await sessionRepo.UpdateLastSeenAtAsync(sessionId, DateTime.UtcNow);

        context.Items[Endpoints.RequestContext.ParticipantIdKey] = session.ParticipantId;
        context.Items[Endpoints.RequestContext.WorkspaceIdKey] = session.WorkspaceId;

        await next(context);
    }

    private static Task Unauthorized(HttpContext context, string code, string message)
        => ApiErrorWriter.WriteAsync(context, StatusCodes.Status401Unauthorized, code, message);
}
