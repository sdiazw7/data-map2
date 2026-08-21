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
            // 410, matching InviteExpiredException above: both mean the link is permanently
            // dead and no client action revives it. 409 is reserved for conflicts the caller
            // can resolve and retry (a stale version, a name already taken).
            await WriteErrorAsync(context, StatusCodes.Status410Gone, "INVITE_USAGE_EXCEEDED", ex.Message);
        }
        catch (VersionConflictException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "VERSION_CONFLICT", ex.Message);
        }
        catch (TemplateWorkspaceNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "TEMPLATE_WORKSPACE_NOT_FOUND", ex.Message);
        }
        catch (WorkspaceNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "WORKSPACE_NOT_FOUND", ex.Message);
        }
        catch (ColumnNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "COLUMN_NOT_FOUND", ex.Message);
        }
        catch (BusinessTermNotFoundException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "BUSINESS_TERM_NOT_FOUND", ex.Message);
        }
        catch (BusinessTermAlreadyExistsException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "BUSINESS_TERM_ALREADY_EXISTS", ex.Message);
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
