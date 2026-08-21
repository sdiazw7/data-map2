using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Services;

namespace DataMap.Api.Endpoints;

public static class ImportEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/imports", async (IFormFile? file, HttpContext ctx, IMetadataImportService svc) =>
        {
            // Binding the file as a parameter leaves the endpoint with routing and a presence
            // check; reaching into ctx.Request.Form put form parsing in the endpoint instead.
            if (file is null)
                throw new ValidationException("No file uploaded.");

            await using var content = file.OpenReadStream();
            var summary = await svc.ImportCsvAsync(ctx.WorkspaceId(), ctx.ParticipantId(),
                new CsvUpload(content, file.FileName, file.Length));

            // 200 rather than 201: an import is not addressable afterwards, so there is no
            // Location to point a caller at. The summary is the whole result.
            return Results.Ok(summary);
        })
        .DisableAntiforgery()
        .WithName("ImportCsv")
        .WithTags("Imports")
        .WithSummary("Imports column metadata from a CSV file.")
        .Produces<ImportSummary>()
        .ProducesAuthErrors()
        .ProducesApiErrors(StatusCodes.Status400BadRequest);
    }
}
