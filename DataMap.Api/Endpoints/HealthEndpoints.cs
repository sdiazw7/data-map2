namespace DataMap.Api.Endpoints;

public record HealthResponse(string Status);

public static class HealthEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
            .AllowAnonymous()
            .WithName("GetHealth")
            .WithTags("Health")
            .WithSummary("Liveness probe.")
            .Produces<HealthResponse>();
    }
}
