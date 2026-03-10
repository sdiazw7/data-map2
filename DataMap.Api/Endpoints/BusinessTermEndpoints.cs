using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class BusinessTermEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/business-terms", async (HttpContext ctx, IBusinessTermService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var result = await svc.GetAllAsync(workspaceId);
            return Results.Ok(result);
        });

        app.MapPost("/business-terms", async (HttpContext ctx, BusinessTermCreateRequest req, IBusinessTermService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var result = await svc.CreateAsync(workspaceId, req);
            return Results.Created($"/business-terms/{result.Id}", result);
        });

        app.MapPost("/business-terms/map", async (HttpContext ctx, TermMappingRequest req, IBusinessTermService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            await svc.MapToColumnAsync(workspaceId, req);
            return Results.Ok();
        });
    }
}
