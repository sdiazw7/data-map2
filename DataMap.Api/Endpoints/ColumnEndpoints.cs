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
        .WithSummary("Applies a batch of column edits, returning each applied column's new version and each stale one as a conflict.")
        .Produces<BulkUpdateResponse>()
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status409Conflict);

        app.MapGet("/columns/{columnId:guid}/changes", async (
            Guid columnId,
            HttpContext ctx,
            IMetadataService svc,
            int limit = 50,
            int offset = 0) =>
        {
            var result = await svc.GetColumnHistoryAsync(ctx.WorkspaceId(), columnId, new PageQuery(limit, offset));
            return Results.Ok(result);
        })
        .WithName("ListColumnChanges")
        .WithTags("Columns")
        .WithSummary("Lists the column's recorded edits, newest first.")
        .Produces<PagedResult<MetadataChangeDto>>()
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound);

        app.MapPut("/columns/{columnId:guid}/business-term", async (
            Guid columnId,
            BusinessTermMappingRequest req,
            HttpContext ctx,
            IBusinessTermService svc) =>
        {
            var result = await svc.MapToColumnAsync(ctx.WorkspaceId(), ctx.ParticipantId(), columnId, req.TermId);
            return Results.Ok(result);
        })
        .WithName("SetColumnBusinessTerm")
        .WithTags("Columns")
        .WithSummary("Assigns a business term to the column, replacing any existing assignment, and returns the column's new version.")
        .Produces<ColumnVersionDto>()
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status404NotFound,
            StatusCodes.Status409Conflict);

        app.MapDelete("/columns/{columnId:guid}/business-term", async (
            Guid columnId,
            HttpContext ctx,
            IBusinessTermService svc) =>
        {
            var result = await svc.UnmapFromColumnAsync(ctx.WorkspaceId(), ctx.ParticipantId(), columnId);
            return Results.Ok(result);
        })
        .WithName("ClearColumnBusinessTerm")
        .WithTags("Columns")
        .WithSummary("Clears the column's business term and returns the column's new version.")
        .Produces<ColumnVersionDto>()
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status404NotFound,
            StatusCodes.Status409Conflict);

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
