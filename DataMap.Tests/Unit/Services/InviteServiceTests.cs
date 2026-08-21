using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

namespace DataMap.Tests.Unit.Services;

public class InviteServiceTests
{
    private readonly Mock<IInviteRepository> _inviteRepo = new();
    private readonly Mock<IParticipantRepository> _participantRepo = new();
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<IWorkspaceRepository> _workspaceRepo = new();
    private readonly Mock<IWorkspaceCopyService> _workspaceCopyService = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<InviteService>> _logger = new();

    public InviteServiceTests()
    {
        // Run the transactional body inline; committing is EF's concern, not the service's.
        _unitOfWork
            .Setup(u => u.ExecuteAsync(It.IsAny<Func<Task<(Participant, Guid, ParticipantSession)>>>()))
            .Returns<Func<Task<(Participant, Guid, ParticipantSession)>>>(operation => operation());
    }

    private InviteService CreateService() => new(
        _inviteRepo.Object,
        _participantRepo.Object,
        _sessionRepo.Object,
        _workspaceRepo.Object,
        _workspaceCopyService.Object,
        _httpContextAccessor.Object,
        _unitOfWork.Object,
        _logger.Object);

    private static Invite MakeInvite(int usedCount = 0, int maxUses = 10, DateTime? expiresAt = null)
    {
        var workspaceId = Guid.NewGuid();
        return new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Token = "token",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(30),
            MaxUses = maxUses,
            UsedCount = usedCount,
            Workspace = new Workspace { Id = workspaceId, Name = "Test Workspace", CreatedAt = DateTime.UtcNow }
        };
    }

    // ── GetAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ValidToken_ReturnsInviteDto()
    {
        var invite = MakeInvite();
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        var result = await CreateService().GetAsync("token");

        Assert.Equal(invite.Id, result.Id);
        Assert.Equal(invite.WorkspaceId, result.WorkspaceId);
        Assert.Equal("Test Workspace", result.WorkspaceName);
        Assert.Equal(invite.ExpiresAt, result.ExpiresAt);
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GetAsync_TokenNotFound_ThrowsInviteNotFoundException()
    {
        _inviteRepo.Setup(r => r.GetByTokenAsync("missing")).ReturnsAsync((Invite?)null);

        await Assert.ThrowsAsync<InviteNotFoundException>(() => CreateService().GetAsync("missing"));
    }

    [Fact]
    public async Task GetAsync_ExpiredInvite_ReturnsIsValidFalse()
    {
        var invite = MakeInvite(expiresAt: DateTime.UtcNow.AddSeconds(-1));
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        var result = await CreateService().GetAsync("token");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetAsync_UsedCountEqualsMaxUses_ReturnsIsValidFalse()
    {
        var invite = MakeInvite(usedCount: 10, maxUses: 10);
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        var result = await CreateService().GetAsync("token");

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task GetAsync_UsedCountExceedsMaxUses_ReturnsIsValidFalse()
    {
        var invite = MakeInvite(usedCount: 11, maxUses: 10);
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        var result = await CreateService().GetAsync("token");

        Assert.False(result.IsValid);
    }

    // ── JoinAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinAsync_NewParticipant_ReturnsJoinResponse()
    {
        var invite = MakeInvite();
        Participant? created = null;

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "new@example.com")).ReturnsAsync((Participant?)null);
        // Real repositories persist and return the same instance they were handed.
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>()))
            .Callback<Participant>(p => created = p)
            .ReturnsAsync((Participant p) => p);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        var result = await CreateService().JoinAsync("token", new JoinRequest("new@example.com"));

        Assert.Equal(created!.Id, result.ParticipantId);
        Assert.Equal(invite.WorkspaceId, result.WorkspaceId);
        Assert.Equal("Test Workspace", result.WorkspaceName);
        Assert.Equal("new@example.com", result.Email);
    }

    [Fact]
    public async Task JoinAsync_NewParticipant_IncrementsUsedCount()
    {
        var invite = MakeInvite();
        var created = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "new@example.com", InviteId = invite.Id };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "new@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync(created);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        await CreateService().JoinAsync("token", new JoinRequest("new@example.com"));

        _inviteRepo.Verify(r => r.IncrementUsedCountAsync(invite.Id), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_ExistingParticipant_UpdatesLastSeenAtDoesNotCreateParticipant()
    {
        var invite = MakeInvite();
        var existing = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "existing@example.com", InviteId = invite.Id };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "existing@example.com")).ReturnsAsync(existing);
        _participantRepo.Setup(r => r.UpdateLastSeenAtAsync(existing.Id, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        await CreateService().JoinAsync("token", new JoinRequest("existing@example.com"));

        _participantRepo.Verify(r => r.UpdateLastSeenAtAsync(existing.Id, It.IsAny<DateTime>()), Times.Once);
        _participantRepo.Verify(r => r.CreateAsync(It.IsAny<Participant>()), Times.Never);
        _inviteRepo.Verify(r => r.IncrementUsedCountAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_AlwaysCreatesSession()
    {
        var invite = MakeInvite();
        var existing = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "existing@example.com", InviteId = invite.Id };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "existing@example.com")).ReturnsAsync(existing);
        _participantRepo.Setup(r => r.UpdateLastSeenAtAsync(existing.Id, It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

        await CreateService().JoinAsync("token", new JoinRequest("existing@example.com"));

        _sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ParticipantSession>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_SetsParticipantSessionCookieOnResponse()
    {
        var invite = MakeInvite();
        var created = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "cookie@example.com", InviteId = invite.Id };
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "cookie@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync(created);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        await CreateService().JoinAsync("token", new JoinRequest("cookie@example.com"));

        Assert.Contains("participant_session", httpContext.Response.Headers["Set-Cookie"].ToString());
    }

    [Fact]
    public async Task JoinAsync_TokenNotFound_ThrowsInviteNotFoundException()
    {
        _inviteRepo.Setup(r => r.GetByTokenAsync("bad")).ReturnsAsync((Invite?)null);

        await Assert.ThrowsAsync<InviteNotFoundException>(() =>
            CreateService().JoinAsync("bad", new JoinRequest("test@example.com")));
    }

    [Fact]
    public async Task JoinAsync_ExpiredInvite_ThrowsInviteExpiredException()
    {
        var invite = MakeInvite(expiresAt: DateTime.UtcNow.AddSeconds(-1));
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        await Assert.ThrowsAsync<InviteExpiredException>(() =>
            CreateService().JoinAsync("token", new JoinRequest("test@example.com")));
    }

    [Fact]
    public async Task JoinAsync_MaxUsesReached_ThrowsInviteUsageExceededException()
    {
        var invite = MakeInvite(usedCount: 10, maxUses: 10);
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        await Assert.ThrowsAsync<InviteUsageExceededException>(() =>
            CreateService().JoinAsync("token", new JoinRequest("test@example.com")));
    }

    [Fact]
    public async Task JoinAsync_EmptyEmail_ThrowsValidationException()
    {
        var invite = MakeInvite();
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().JoinAsync("token", new JoinRequest("")));
    }

    [Fact]
    public async Task JoinAsync_WhitespaceEmail_ThrowsValidationException()
    {
        var invite = MakeInvite();
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().JoinAsync("token", new JoinRequest("   ")));
    }

    [Fact]
    public async Task JoinAsync_NullHttpContext_ThrowsInvalidOperationException()
    {
        var invite = MakeInvite();
        var created = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "test@example.com", InviteId = invite.Id };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "test@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync(created);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>())).ReturnsAsync(new ParticipantSession { Id = Guid.NewGuid() });
        _httpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().JoinAsync("token", new JoinRequest("test@example.com")));
    }
}
