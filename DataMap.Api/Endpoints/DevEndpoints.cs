using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

// Only mapped when the host environment is Development — see Program.cs.
public static class DevEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/dev/workspaces", async (IDevAccessService svc) =>
        {
            var result = await svc.ListWorkspacesAsync();
            return Results.Ok(result);
        });

        app.MapPost("/dev/workspaces/{id}/join", async (Guid id, IDevAccessService svc, HttpContext ctx) =>
        {
            var result = await svc.JoinAsync(id);
            SessionCookie.Issue(ctx, result.SessionId);
            return Results.Ok(result.Response);
        });
    }
}
