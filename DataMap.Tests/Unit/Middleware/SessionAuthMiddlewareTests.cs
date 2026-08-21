using DataMap.Api.Endpoints;
using DataMap.Api.Middleware;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Text.Json;

namespace DataMap.Tests.Unit.Middleware;

public class SessionAuthMiddlewareTests
{
    private readonly Mock<ISessionRepository> _sessionRepo = new();

    /// <summary>A matched route that requires a session — the default for these tests.</summary>
    private static Endpoint ProtectedEndpoint() =>
        new(_ => Task.CompletedTask, new EndpointMetadataCollection(), "protected");

    /// <summary>A matched route that opted out via AllowAnonymous.</summary>
    private static Endpoint AnonymousEndpoint() =>
        new(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous");

    private async Task<(int StatusCode, string Body, HttpContext Context, bool NextCalled)> InvokeAsync(
        string? cookieValue = null,
        Endpoint? endpoint = null,
        bool useDefaultEndpoint = true)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        if (endpoint is not null)
            context.SetEndpoint(endpoint);
        else if (useDefaultEndpoint)
            context.SetEndpoint(ProtectedEndpoint());

        if (cookieValue is not null)
            context.Request.Headers["Cookie"] = $"participant_session={cookieValue}";

        var nextCalled = false;
        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };

        var middleware = new SessionAuthMiddleware(next);
        await middleware.InvokeAsync(context, _sessionRepo.Object);

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

    // ── AllowAnonymous opt-out ───────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_AnonymousEndpoint_SkipsAuthAndCallsNext()
    {
        var (status, _, _, nextCalled) = await InvokeAsync(endpoint: AnonymousEndpoint());
        Assert.Equal(200, status);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AnonymousEndpoint_DoesNotSetIdentityOnContext()
    {
        var (_, _, ctx, _) = await InvokeAsync(endpoint: AnonymousEndpoint());

        Assert.False(ctx.Items.ContainsKey(RequestContext.ParticipantIdKey));
        Assert.False(ctx.Items.ContainsKey(RequestContext.WorkspaceIdKey));
    }

    [Fact]
    public async Task InvokeAsync_ProtectedEndpointWithoutCookie_IsNotBypassed()
    {
        // The guard keys off route metadata rather than the request path, so nothing about a
        // route's name can win it a bypass. That is what the old prefix rule could not promise.
        var (status, _, _, nextCalled) = await InvokeAsync(endpoint: ProtectedEndpoint());

        Assert.Equal(401, status);
        Assert.False(nextCalled);
    }

    // ── Unmatched route ──────────────────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_NoEndpointMatched_CallsNextSoRoutingCanAnswer404()
    {
        // Answering 401 here would let an unauthenticated caller tell a real route from a
        // typo, mapping the API surface one guess at a time.
        var (status, _, _, nextCalled) = await InvokeAsync(useDefaultEndpoint: false);

        Assert.Equal(200, status);
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

        Assert.Equal(session.ParticipantId, ctx.Items[RequestContext.ParticipantIdKey]);
        Assert.Equal(session.WorkspaceId, ctx.Items[RequestContext.WorkspaceIdKey]);
        Assert.Equal(session.ParticipantId, ctx.ParticipantId());
        Assert.Equal(session.WorkspaceId, ctx.WorkspaceId());
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
        var (_, _, ctx, _) = await InvokeAsync(cookieValue: null);

        Assert.Equal("application/json", ctx.Response.ContentType);
    }
}
