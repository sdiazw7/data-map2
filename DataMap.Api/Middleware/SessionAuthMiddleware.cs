using System.Text.Json;
using DataMap.Api.Repositories;

namespace DataMap.Api.Middleware;

public class SessionAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ISessionRepository sessionRepo, IWebHostEnvironment env)
    {
        // Skip auth for public paths. /dev is only ever mapped in Development (see Program.cs),
        // but the environment check here means the bypass is inert even if that ever changes.
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/invite") ||
            (env.IsDevelopment() && context.Request.Path.StartsWithSegments("/dev")))
        {
            await next(context);
            return;
        }

        if (!context.Request.Cookies.TryGetValue("participant_session", out var cookieValue)
            || string.IsNullOrWhiteSpace(cookieValue))
        {
            await WriteUnauthorizedAsync(context, "UNAUTHORIZED", "Authentication required.");
            return;
        }

        if (!Guid.TryParse(cookieValue, out var sessionId))
        {
            await WriteUnauthorizedAsync(context, "UNAUTHORIZED", "Authentication required.");
            return;
        }

        var session = await sessionRepo.GetByIdAsync(sessionId);
        if (session is null)
        {
            await WriteUnauthorizedAsync(context, "UNAUTHORIZED", "Authentication required.");
            return;
        }

        if (session.LastSeenAt < DateTime.UtcNow.AddDays(-30))
        {
            await WriteUnauthorizedAsync(context, "SESSION_EXPIRED", "Your session has expired. Please sign in again.");
            return;
        }

        await sessionRepo.UpdateLastSeenAtAsync(sessionId, DateTime.UtcNow);

        context.Items["ParticipantId"] = session.ParticipantId;
        context.Items["WorkspaceId"] = session.WorkspaceId;

        await next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            error = new { code, message }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await context.Response.WriteAsync(body);
    }
}
