using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class InviteEndpoints
{
    public static void Map(WebApplication app)
    {
        // Public: following an invite link is how a participant gets a session in the first
        // place, so these two cannot require one. Marked per-route rather than by path prefix —
        // a prefix rule would silently cover POST /invites, which creates them.
        app.MapGet("/invites/{token}", async (string token, IInviteService svc) =>
        {
            var result = await svc.GetAsync(token);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetInvite")
        .WithTags("Invites")
        .WithSummary("Looks up an invite by its token.")
        .Produces<InviteDto>()
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        app.MapPost("/invites/{token}/join", async (string token, JoinRequest req, IInviteService svc, HttpContext ctx) =>
        {
            var result = await svc.JoinAsync(token, req);
            SessionCookie.Issue(ctx, result.SessionId);
            return Results.Ok(result.Response);
        })
        .AllowAnonymous()
        .WithName("JoinInvite")
        .WithTags("Invites")
        .WithSummary("Redeems an invite and issues a participant session.")
        .Produces<JoinResponse>()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound,
            StatusCodes.Status410Gone);

        app.MapPost("/invites", async (CreateInviteRequest req, IInviteService svc, HttpContext ctx) =>
        {
            var result = await svc.CreateAsync(req, ctx.WorkspaceId());
            return Results.CreatedAtRoute("GetInvite", new { token = result.Token }, result);
        })
        .WithName("CreateInvite")
        .WithTags("Invites")
        .WithSummary("Creates an invite for the caller's workspace.")
        .Produces<CreateInviteResponse>(StatusCodes.Status201Created)
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound);
    }
}
