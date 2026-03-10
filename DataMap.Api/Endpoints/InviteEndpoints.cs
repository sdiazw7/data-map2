using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class InviteEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/invite/{token}", async (string token, IInviteService svc) =>
        {
            var result = await svc.GetAsync(token);
            return Results.Ok(result);
        });

        app.MapPost("/invite/{token}/join", async (string token, JoinRequest req, IInviteService svc) =>
        {
            var result = await svc.JoinAsync(token, req);
            return Results.Ok(result);
        });

        app.MapPost("/invites", async (CreateInviteRequest req, IInviteService svc, HttpContext ctx) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var result = await svc.CreateAsync(req, workspaceId);
            return Results.Created($"/invite/{result.Token}", result);
        });
    }
}
