using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class ColumnEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/columns", async (
            HttpContext ctx,
            IMetadataService svc,
            int limit = 200,
            int offset = 0,
            string? search = null,
            bool undocumentedOnly = false,
            string? tableName = null,
            string sortBy = "columnName",
            string sortDir = "asc") =>
        {
            var query = new MetadataColumnsQuery(limit, offset, search, undocumentedOnly, tableName, sortBy, sortDir);
            var result = await svc.GetColumnsAsync(ctx.WorkspaceId(), query);
            return Results.Ok(result);
        })
        .WithName("ListColumns")
        .WithTags("Columns")
        .WithSummary("Lists the workspace's columns, filtered and paged.")
        .Produces<PagedResult<ColumnGridRow>>()
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status400BadRequest);

        app.MapPatch("/columns", async (
            HttpContext ctx,
            List<ColumnUpdateRequest> req,
            IMetadataService svc) =>
        {
            var result = await svc.BulkUpdateAsync(ctx.WorkspaceId(), ctx.ParticipantId(), req);
            return Results.Ok(result);
        })
        .WithName("BulkUpdateColumns")
        .WithTags("Columns")
        .WithSummary("Applies a batch of column edits and returns each column's new version.")
        .Produces<BulkUpdateResponse>()
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status409Conflict);

        app.MapPut("/columns/{columnId:guid}/business-term", async (
            Guid columnId,
            BusinessTermMappingRequest req,
            HttpContext ctx,
            IBusinessTermService svc) =>
        {
            await svc.MapToColumnAsync(ctx.WorkspaceId(), columnId, req.TermId);
            return Results.NoContent();
        })
        .WithName("SetColumnBusinessTerm")
        .WithTags("Columns")
        .WithSummary("Assigns a business term to the column, replacing any existing assignment.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound);

        app.MapDelete("/columns/{columnId:guid}/business-term", async (
            Guid columnId,
            HttpContext ctx,
            IBusinessTermService svc) =>
        {
            await svc.UnmapFromColumnAsync(ctx.WorkspaceId(), columnId);
            return Results.NoContent();
        })
        .WithName("ClearColumnBusinessTerm")
        .WithTags("Columns")
        .WithSummary("Clears the column's business term.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        app.MapGet("/tables", async (HttpContext ctx, IMetadataService svc, int limit = 500, int offset = 0) =>
        {
            var result = await svc.GetTableNamesAsync(ctx.WorkspaceId(), new PageQuery(limit, offset));
            return Results.Ok(result);
        })
        .WithName("ListTables")
        .WithTags("Tables")
        .WithSummary("Lists the distinct table names in the workspace.")
        .Produces<PagedResult<string>>()
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status400BadRequest);

        app.MapGet("/coverage", async (HttpContext ctx, IMetadataService svc) =>
        {
            var result = await svc.GetCoverageAsync(ctx.WorkspaceId());
            return Results.Ok(result);
        })
        .WithName("GetCoverage")
        .WithTags("Coverage")
        .WithSummary("Reports how much of the workspace's catalog is documented.")
        .Produces<CoverageResponse>()
        .ProducesAuthErrors();
    }
}
