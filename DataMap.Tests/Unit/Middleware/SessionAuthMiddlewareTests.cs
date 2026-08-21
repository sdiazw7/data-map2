using DataMap.Api.Middleware;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Text.Json;

namespace DataMap.Tests.Unit.Middleware;

public class SessionAuthMiddlewareTests
{
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<IWebHostEnvironment> _env = new();

    public SessionAuthMiddlewareTests()
    {
        // Non-Development, so the /dev path bypass stays inert for these tests.
        _env.SetupGet(e => e.EnvironmentName).Returns(Environments.Production);
    }

    private async Task<(int StatusCode, string Body, HttpContext Context, bool NextCalled)> InvokeAsync(
        string path = "/metadata/columns",
        string? cookieValue = null)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;

        if (cookieValue is not null)
            context.Request.Headers["Cookie"] = $"participant_session={cookieValue}";

        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new SessionAuthMiddleware(next);
        await middleware.InvokeAsync(context, _sessionRepo.Object, _env.Object);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (context.Response.StatusCode, body, context, nextCalled);
    }

    private static ParticipantSession ValidSession(DateTime? lastSeenAt = null) => new()
    {
        Id = Guid.NewGuid(),
        ParticipantId = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        LastSeenAt = lastSeenAt ?? DateTime.UtcNow
    };

    private static string GetErrorCode(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetProperty("code").GetString()!;

    // ── Invite path bypass ───────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_InvitePath_SkipsAuthAndCallsNext()
    {
        var (status, _, _, nextCalled) = await InvokeAsync(path: "/invite/abc123");
        Assert.Equal(200, status);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_InviteJoinPath_SkipsAuth()
    {
        var (_, _, _, nextCalled) = await InvokeAsync(path: "/invite/abc/join");
        Assert.True(nextCalled);
    }

    // ── Valid session ────────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_ValidSession_CallsNext()
    {
        var session = ValidSession();
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);
        _sessionRepo.Setup(r => r.UpdateLastSeenAtAsync(session.Id, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var (_, _, _, nextCalled) = await InvokeAsync(cookieValue: session.Id.ToString());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ValidSession_SetsParticipantIdAndWorkspaceIdOnContext()
    {
        var session = ValidSession();
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);
        _sessionRepo.Setup(r => r.UpdateLastSeenAtAsync(session.Id, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        var (_, _, ctx, _) = await InvokeAsync(cookieValue: session.Id.ToString());

        Assert.Equal(session.ParticipantId, ctx.Items["ParticipantId"]);
        Assert.Equal(session.WorkspaceId, ctx.Items["WorkspaceId"]);
    }

    [Fact]
    public async Task InvokeAsync_ValidSession_UpdatesLastSeenAt()
    {
        var session = ValidSession();
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);
        _sessionRepo.Setup(r => r.UpdateLastSeenAtAsync(session.Id, It.IsAny<DateTime>())).Returns(Task.CompletedTask);

        await InvokeAsync(cookieValue: session.Id.ToString());

        _sessionRepo.Verify(r => r.UpdateLastSeenAtAsync(session.Id, It.IsAny<DateTime>()), Times.Once);
    }

    // ── Missing / malformed cookie ───────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NoCookie_Returns401()
    {
        var (status, body, _, nextCalled) = await InvokeAsync(cookieValue: null);
        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", GetErrorCode(body));
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_EmptyCookieValue_Returns401()
    {
        var (status, body, _, _) = await InvokeAsync(cookieValue: "");
        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_NonGuidCookie_Returns401()
    {
        var (status, body, _, _) = await InvokeAsync(cookieValue: "not-a-guid");
        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", GetErrorCode(body));
    }

    // ── Session lookup failures ──────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_SessionNotFoundInDb_Returns401()
    {
        _sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((ParticipantSession?)null);

        var (status, body, _, _) = await InvokeAsync(cookieValue: Guid.NewGuid().ToString());

        Assert.Equal(401, status);
        Assert.Equal("UNAUTHORIZED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_ExpiredSession_Returns401WithSessionExpiredCode()
    {
        var session = ValidSession(lastSeenAt: DateTime.UtcNow.AddDays(-31));
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);

        var (status, body, _, _) = await InvokeAsync(cookieValue: session.Id.ToString());

        Assert.Equal(401, status);
        Assert.Equal("SESSION_EXPIRED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_SessionExactlyAt30DaysOld_IsExpired()
    {
        // LastSeenAt < UtcNow.AddDays(-30) means expired. Exactly -30 days is NOT expired.
        var session = ValidSession(lastSeenAt: DateTime.UtcNow.AddDays(-30).AddSeconds(-1));
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);

        var (status, body, _, _) = await InvokeAsync(cookieValue: session.Id.ToString());

        Assert.Equal(401, status);
        Assert.Equal("SESSION_EXPIRED", GetErrorCode(body));
    }

    [Fact]
    public async Task InvokeAsync_ExpiredSession_DoesNotCallNext()
    {
        var session = ValidSession(lastSeenAt: DateTime.UtcNow.AddDays(-31));
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id)).ReturnsAsync(session);

        var (_, _, _, nextCalled) = await InvokeAsync(cookieValue: session.Id.ToString());

        Assert.False(nextCalled);
    }

    // ── Response format ──────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_Unauthorized_SetsContentTypeToJson()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/metadata/columns";

        var middleware = new SessionAuthMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(context, _sessionRepo.Object, _env.Object);

        Assert.Equal("application/json", context.Response.ContentType);
    }
}
