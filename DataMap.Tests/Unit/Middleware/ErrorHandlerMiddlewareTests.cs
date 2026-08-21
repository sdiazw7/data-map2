using DataMap.Api.Exceptions;
using DataMap.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace DataMap.Tests.Unit.Middleware;

public class ErrorHandlerMiddlewareTests
{
    private readonly Mock<ILogger<ErrorHandlerMiddleware>> _logger = new();

    private async Task<(int StatusCode, string Body)> InvokeAsync(Exception? toThrow = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        RequestDelegate next = toThrow is null
            ? _ => Task.CompletedTask
            : _ => throw toThrow;

        var middleware = new ErrorHandlerMiddleware(next, _logger.Object);
        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    private static string GetErrorCode(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("code").GetString()!;

    private static string GetErrorMessage(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("message").GetString()!;

    // ── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NoException_PassesThroughWith200()
    {
        var (status, _) = await InvokeAsync();
        Assert.Equal(200, status);
    }

    // ── Domain exceptions ────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_InviteNotFoundException_Returns404WithCode()
    {
        var (status, body) = await InvokeAsync(new InviteNotFoundException());
        Assert.Equal(404, status);
        Assert.Equal("INVITE_NOT_FOUND", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_InviteExpiredException_Returns410WithCode()
    {
        var (status, body) = await InvokeAsync(new InviteExpiredException());
        Assert.Equal(410, status);
        Assert.Equal("INVITE_EXPIRED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_InviteUsageExceededException_Returns410WithCode()
    {
        var (status, body) = await InvokeAsync(new InviteUsageExceededException());
        Assert.Equal(410, status);
        Assert.Equal("INVITE_USAGE_EXCEEDED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_VersionConflictException_Returns409WithCode()
    {
        var (status, body) = await InvokeAsync(new VersionConflictException());
        Assert.Equal(409, status);
        Assert.Equal("VERSION_CONFLICT", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_Returns400WithCode()
    {
        var (status, body) = await InvokeAsync(new ValidationException("Name is required."));
        Assert.Equal(400, status);
        Assert.Equal("VALIDATION_ERROR", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_ValidationException_IncludesCustomMessage()
    {
        var (_, body) = await InvokeAsync(new ValidationException("Term name is required."));
        Assert.Equal("Term name is required.", GetErrorMessage(body));
    }

    // ── Unhandled exception ──────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithCode()
    {
        var (status, body) = await InvokeAsync(new Exception("Something went wrong"));
        Assert.Equal(500, status);
        Assert.Equal("INTERNAL_ERROR", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_DoesNotLeakInternalMessage()
    {
        var (_, body) = await InvokeAsync(new Exception("sensitive internal detail"));
        Assert.DoesNotContain("sensitive internal detail", body);
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_LogsErrorWithMethodAndPath()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/metadata/columns";

        var middleware = new ErrorHandlerMiddleware(
            _ => throw new Exception("boom"),
            _logger.Object);

        await middleware.InvokeAsync(context);

        _logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // ── Response format ──────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_OnError_SetsContentTypeToJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ErrorHandlerMiddleware(
            _ => throw new ValidationException("test"),
            _logger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal("application/json", context.Response.ContentType);
    }

    [Fact]
    public async Task InvokeAsync_ResponseBodyIsValidJson()
    {
        var (_, body) = await InvokeAsync(new ValidationException("bad input"));
        var doc = JsonDocument.Parse(body); // throws if invalid JSON
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("error").ValueKind);
    }
}
