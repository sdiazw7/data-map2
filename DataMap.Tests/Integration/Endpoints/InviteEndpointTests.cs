using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Services;
using DataMap.Tests.Integration;
using Moq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DataMap.Tests.Integration.Endpoints;

public class InviteEndpointTests(TestFixture fixture) : IClassFixture<TestFixture>
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── GET /invite/{token} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetInvite_ValidToken_Returns200WithInviteDto()
    {
        var inviteDto = new InviteDto(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp", DateTime.UtcNow.AddDays(30), true);
        fixture.InviteService.Setup(s => s.GetAsync("demo")).ReturnsAsync(inviteDto);

        var client = fixture.CreateClient();
        var response = await client.GetAsync("/invite/demo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InviteDto>(JsonOpts);
        Assert.Equal(inviteDto.Id, result!.Id);
        Assert.Equal("Acme Corp", result.WorkspaceName);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GetInvite_TokenNotFound_Returns404()
    {
        fixture.InviteService.Setup(s => s.GetAsync("bad-token")).ThrowsAsync(new InviteNotFoundException());

        var client = fixture.CreateClient();
        var response = await client.GetAsync("/invite/bad-token");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "INVITE_NOT_FOUND");
    }

    [Fact]
    public async Task GetInvite_ExpiredInvite_Returns200WithIsValidFalse()
    {
        var inviteDto = new InviteDto(Guid.NewGuid(), Guid.NewGuid(), "Acme", DateTime.UtcNow.AddDays(-1), false);
        fixture.InviteService.Setup(s => s.GetAsync("expired")).ReturnsAsync(inviteDto);

        var client = fixture.CreateClient();
        var response = await client.GetAsync("/invite/expired");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<InviteDto>(JsonOpts);
        Assert.False(result!.IsValid);
    }

    // ── POST /invite/{token}/join ────────────────────────────────────────────

    [Fact]
    public async Task JoinInvite_ValidRequest_Returns200WithJoinResponse()
    {
        var joinResponse = new JoinResponse(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp", "user@example.com");
        fixture.InviteService.Setup(s => s.JoinAsync("demo", It.IsAny<JoinRequest>()))
            .ReturnsAsync(new JoinResult(joinResponse, Guid.NewGuid()));

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/invite/demo/join", new { email = "user@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JoinResponse>(JsonOpts);
        Assert.Equal("user@example.com", result!.Email);
        Assert.Equal("Acme Corp", result.WorkspaceName);
    }

    [Fact]
    public async Task JoinInvite_TokenNotFound_Returns404()
    {
        fixture.InviteService.Setup(s => s.JoinAsync("missing", It.IsAny<JoinRequest>()))
            .ThrowsAsync(new InviteNotFoundException());

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/invite/missing/join", new { email = "user@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JoinInvite_ExpiredInvite_Returns410()
    {
        fixture.InviteService.Setup(s => s.JoinAsync("expired", It.IsAny<JoinRequest>()))
            .ThrowsAsync(new InviteExpiredException());

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/invite/expired/join", new { email = "user@example.com" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "INVITE_EXPIRED");
    }

    [Fact]
    public async Task JoinInvite_MaxUsesReached_Returns410()
    {
        fixture.InviteService.Setup(s => s.JoinAsync("full", It.IsAny<JoinRequest>()))
            .ThrowsAsync(new InviteUsageExceededException());

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/invite/full/join", new { email = "user@example.com" });

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "INVITE_USAGE_EXCEEDED");
    }

    [Fact]
    public async Task JoinInvite_EmptyEmail_Returns400()
    {
        fixture.InviteService.Setup(s => s.JoinAsync("demo", It.IsAny<JoinRequest>()))
            .ThrowsAsync(new ValidationException("Email is required."));

        var client = fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/invite/demo/join", new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertErrorCode(await response.Content.ReadAsStringAsync(), "VALIDATION_ERROR");
    }

    private static void AssertErrorCode(string body, string expectedCode)
    {
        var doc = JsonDocument.Parse(body);
        var code = doc.RootElement.GetProperty("error").GetProperty("code").GetString();
        Assert.Equal(expectedCode, code);
    }
}
