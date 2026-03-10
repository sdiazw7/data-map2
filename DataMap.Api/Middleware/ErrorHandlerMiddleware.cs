using System.Text.Json;
using DataMap.Api.Exceptions;

namespace DataMap.Api.Middleware;

public class ErrorHandlerMiddleware(RequestDelegate next, ILogger<ErrorHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (InviteNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "INVITE_NOT_FOUND", ex.Message);
        }
        catch (InviteExpiredException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status410Gone, "INVITE_EXPIRED", ex.Message);
        }
        catch (InviteUsageExceededException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "INVITE_USAGE_EXCEEDED", ex.Message);
        }
        catch (VersionConflictException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "VERSION_CONFLICT", ex.Message);
        }
        catch (TemplateWorkspaceNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "TEMPLATE_WORKSPACE_NOT_FOUND", ex.Message);
        }
        catch (ValidationException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            error = new { code, message }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await context.Response.WriteAsync(body);
    }
}
