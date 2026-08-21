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
        catch (UnauthorizedException ex)
        {
            await WriteAsync(context, StatusCodes.Status401Unauthorized, "UNAUTHORIZED", ex.Message);
        }
        catch (InviteNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "INVITE_NOT_FOUND", ex.Message);
        }
        catch (InviteExpiredException ex)
        {
            await WriteAsync(context, StatusCodes.Status410Gone, "INVITE_EXPIRED", ex.Message);
        }
        catch (InviteUsageExceededException ex)
        {
            // 410, matching InviteExpiredException above: both mean the link is permanently
            // dead and no client action revives it. 409 is reserved for conflicts the caller
            // can resolve and retry (a stale version, a name already taken).
            await WriteAsync(context, StatusCodes.Status410Gone, "INVITE_USAGE_EXCEEDED", ex.Message);
        }
        catch (VersionConflictException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "VERSION_CONFLICT", ex.Message);
        }
        catch (TemplateWorkspaceNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "TEMPLATE_WORKSPACE_NOT_FOUND", ex.Message);
        }
        catch (WorkspaceNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "WORKSPACE_NOT_FOUND", ex.Message);
        }
        catch (ColumnNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "COLUMN_NOT_FOUND", ex.Message);
        }
        catch (BusinessTermNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, "BUSINESS_TERM_NOT_FOUND", ex.Message);
        }
        catch (BusinessTermAlreadyExistsException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, "BUSINESS_TERM_ALREADY_EXISTS", ex.Message);
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, "VALIDATION_ERROR", ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            // Thrown by model binding before any handler runs — unparseable JSON, a route value
            // that is not a Guid, a body that is too large. These used to escape the custom
            // envelope entirely and surface as the framework's ProblemDetails, which no client
            // of this API knows how to read.
            logger.LogInformation("Malformed request to {Method} {Path}: {Reason}",
                context.Request.Method, context.Request.Path, ex.Message);
            await WriteAsync(context, StatusCodes.Status400BadRequest, "MALFORMED_REQUEST",
                "The request could not be parsed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static Task WriteAsync(HttpContext context, int statusCode, string code, string message)
        => ApiErrorWriter.WriteAsync(context, statusCode, code, message);
}
