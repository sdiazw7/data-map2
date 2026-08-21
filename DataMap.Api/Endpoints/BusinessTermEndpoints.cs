using DataMap.Api.DTOs;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class BusinessTermEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/business-terms", async (HttpContext ctx, IBusinessTermService svc, int limit = 200, int offset = 0) =>
        {
            var result = await svc.GetAllAsync(ctx.WorkspaceId(), new PageQuery(limit, offset));
            return Results.Ok(result);
        })
        .WithName("ListBusinessTerms")
        .WithTags("BusinessTerms")
        .WithSummary("Lists the workspace's business glossary.")
        .Produces<PagedResult<BusinessTermDto>>()
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status400BadRequest);

        app.MapGet("/business-terms/{id:guid}", async (Guid id, HttpContext ctx, IBusinessTermService svc) =>
        {
            var result = await svc.GetByIdAsync(ctx.WorkspaceId(), id);
            return Results.Ok(result);
        })
        .WithName("GetBusinessTerm")
        .WithTags("BusinessTerms")
        .WithSummary("Fetches a single business term.")
        .Produces<BusinessTermDto>()
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status404NotFound);

        app.MapPost("/business-terms", async (HttpContext ctx, BusinessTermCreateRequest req, IBusinessTermService svc) =>
        {
            var result = await svc.CreateAsync(ctx.WorkspaceId(), req);
            return Results.CreatedAtRoute("GetBusinessTerm", new { id = result.Id }, result);
        })
        .WithName("CreateBusinessTerm")
        .WithTags("BusinessTerms")
        .WithSummary("Creates a business term.")
        .Produces<BusinessTermDto>(StatusCodes.Status201Created)
        .ProducesAuthErrors()
        .ProducesApiErrors(
            StatusCodes.Status400BadRequest,
            StatusCodes.Status409Conflict);
    }
}
