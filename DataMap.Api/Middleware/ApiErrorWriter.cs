using System.Text.Json;
using DataMap.Api.DTOs;

namespace DataMap.Api.Middleware;

/// <summary>
/// The one place an error response is serialized. Three copies of this used to exist — two
/// middlewares and an endpoint — each with its own serializer options, which is how a shape
/// drifts. Everything that reports a failure goes through here.
/// </summary>
public static class ApiErrorWriter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        // Nothing can be written once the response is on the wire; overwriting the status here
        // would throw and mask whatever the original failure was.
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = new ApiErrorResponse(new ApiError(code, message));
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, Options));
    }

    /// <summary>
    /// Fallback text for a status the framework produced on its own — a route that matched
    /// nothing, a rejected method, an unsupported content type.
    /// </summary>
    public static (string Code, string Message) DescribeStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ("BAD_REQUEST", "The request was malformed."),
        StatusCodes.Status401Unauthorized => ("UNAUTHORIZED", "Authentication required."),
        StatusCodes.Status403Forbidden => ("FORBIDDEN", "You do not have access to this resource."),
        StatusCodes.Status404NotFound => ("NOT_FOUND", "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => ("METHOD_NOT_ALLOWED", "That method is not allowed on this resource."),
        StatusCodes.Status415UnsupportedMediaType => ("UNSUPPORTED_MEDIA_TYPE", "The request content type is not supported."),
        StatusCodes.Status429TooManyRequests => ("TOO_MANY_REQUESTS", "Too many requests. Please slow down."),
        _ => ("INTERNAL_ERROR", "An unexpected error occurred."),
    };
}
