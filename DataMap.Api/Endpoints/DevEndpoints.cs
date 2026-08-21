using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

// Only mapped when the host environment is Development — see Program.cs.
public static class DevEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/dev/workspaces", async (IDevAccessService svc, int limit = 200, int offset = 0) =>
        {
            var result = await svc.ListWorkspacesAsync(new PageQuery(limit, offset));
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("ListDevWorkspaces")
        .WithTags("Dev")
        .Produces<PagedResult<WorkspaceSummaryDto>>()
        .ProducesApiErrors(StatusCodes.Status400BadRequest);

        app.MapPost("/dev/workspaces/{id:guid}/join", async (Guid id, IDevAccessService svc, HttpContext ctx) =>
        {
            var result = await svc.JoinAsync(id);
            SessionCookie.Issue(ctx, result.SessionId);
            return Results.Ok(result.Response);
        })
        .AllowAnonymous()
        .WithName("JoinDevWorkspace")
        .WithTags("Dev")
        .Produces<JoinResponse>()
        .ProducesApiErrors(StatusCodes.Status404NotFound);
    }
}
