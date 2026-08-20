using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class MetadataEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/metadata/upload", async (HttpContext ctx, IMetadataService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var participantId = (Guid)ctx.Items["ParticipantId"]!;
            var file = ctx.Request.Form.Files.GetFile("file");
            if (file is null) return Results.BadRequest(new { error = new { code = "NO_FILE", message = "No file uploaded." } });
            await svc.UploadCsvAsync(workspaceId, participantId, file);
            return Results.Ok();
        }).DisableAntiforgery();

        app.MapGet("/metadata/columns", async (
            HttpContext ctx,
            IMetadataService svc,
            int limit = 200,
            int offset = 0,
            string? search = null,
            bool undocumented_only = false,
            string? table_name = null) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var query = new MetadataColumnsQuery(limit, offset, search, undocumented_only, table_name);
            var result = await svc.GetColumnsAsync(workspaceId, query);
            return Results.Ok(result);
        });

        app.MapGet("/metadata/tables", async (HttpContext ctx, IMetadataService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var result = await svc.GetTableNamesAsync(workspaceId);
            return Results.Ok(result);
        });

        app.MapMethods("/metadata/columns", ["PATCH"], async (
            HttpContext ctx,
            List<ColumnUpdateRequest> req,
            IMetadataService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var participantId = (Guid)ctx.Items["ParticipantId"]!;
            await svc.BulkUpdateAsync(workspaceId, participantId, req);
            return Results.Ok();
        });

        app.MapGet("/metadata/coverage", async (HttpContext ctx, IMetadataService svc) =>
        {
            var workspaceId = (Guid)ctx.Items["WorkspaceId"]!;
            var result = await svc.GetCoverageAsync(workspaceId);
            return Results.Ok(result);
        });
    }
}
