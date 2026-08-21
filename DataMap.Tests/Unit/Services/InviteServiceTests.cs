using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using DataMap.Api.Services;
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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ILogger<InviteService>> _logger = new();

    public InviteServiceTests()
    {
        // Run the transactional body inline; committing is EF's concern, not the service's.
        _unitOfWork
            .Setup(u => u.ExecuteAsync(It.IsAny<Func<Task<(Participant, Guid, ParticipantSession)>>>()))
            .Returns<Func<Task<(Participant, Guid, ParticipantSession)>>>(operation => operation());

        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>()))
            .ReturnsAsync((ParticipantSession s) => s);
    }

    private InviteService CreateService() => new(
        _inviteRepo.Object,
        _participantRepo.Object,
        _sessionRepo.Object,
        _workspaceRepo.Object,
        _workspaceCopyService.Object,
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

        var result = await CreateService().JoinAsync("token", new JoinRequest("new@example.com"));

        Assert.Equal(created!.Id, result.Response.ParticipantId);
        Assert.Equal(invite.WorkspaceId, result.Response.WorkspaceId);
        Assert.Equal("Test Workspace", result.Response.WorkspaceName);
        Assert.Equal("new@example.com", result.Response.Email);
    }

    [Fact]
    public async Task JoinAsync_ReturnsTheSessionIdForTheEndpointToIssue()
    {
        var invite = MakeInvite();
        ParticipantSession? created = null;

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "new@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync((Participant p) => p);
        _sessionRepo.Setup(r => r.CreateAsync(It.IsAny<ParticipantSession>()))
            .Callback<ParticipantSession>(s => created = s)
            .ReturnsAsync((ParticipantSession s) => s);

        var result = await CreateService().JoinAsync("token", new JoinRequest("new@example.com"));

        // The session id is a credential: it goes to the endpoint to become an HttpOnly cookie,
        // never into the JSON body.
        Assert.Equal(created!.Id, result.SessionId);
    }

    [Fact]
    public async Task JoinAsync_NewParticipant_IncrementsUsedCount()
    {
        var invite = MakeInvite();
        var created = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "new@example.com", InviteId = invite.Id };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "new@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync(created);

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

        await CreateService().JoinAsync("token", new JoinRequest("existing@example.com"));

        _sessionRepo.Verify(r => r.CreateAsync(It.IsAny<ParticipantSession>()), Times.Once);
    }

    [Fact]
    public async Task JoinAsync_RunsTheWholeJoinInOneTransaction()
    {
        var invite = MakeInvite();
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "new@example.com")).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>())).ReturnsAsync((Participant p) => p);

        await CreateService().JoinAsync("token", new JoinRequest("new@example.com"));

        _unitOfWork.Verify(u => u.ExecuteAsync(
            It.IsAny<Func<Task<(Participant, Guid, ParticipantSession)>>>()), Times.Once);
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("two@at@example.com")]
    public async Task JoinAsync_InvalidEmail_ThrowsValidationException(string email)
    {
        var invite = MakeInvite();
        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().JoinAsync("token", new JoinRequest(email)));
    }

    [Theory]
    [InlineData("User@Example.COM")]
    [InlineData("  user@example.com  ")]
    [InlineData("USER@EXAMPLE.COM")]
    public async Task JoinAsync_NormalizesEmailBeforeLookup(string supplied)
    {
        // Participants are keyed by (workspace_id, email) on a case-sensitive index. Without
        // normalization one person would end up holding two participant rows — and on a
        // template invite, two separate private copies of the workspace.
        var invite = MakeInvite();
        var existing = new Participant { Id = Guid.NewGuid(), WorkspaceId = invite.WorkspaceId, Email = "user@example.com" };

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "user@example.com")).ReturnsAsync(existing);

        await CreateService().JoinAsync("token", new JoinRequest(supplied));

        _participantRepo.Verify(r => r.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, "user@example.com"), Times.Once);
        _participantRepo.Verify(r => r.CreateAsync(It.IsAny<Participant>()), Times.Never);
    }

    [Fact]
    public async Task JoinAsync_StoresNormalizedEmailOnNewParticipant()
    {
        var invite = MakeInvite();
        Participant? created = null;

        _inviteRepo.Setup(r => r.GetByTokenAsync("token")).ReturnsAsync(invite);
        _participantRepo.Setup(r => r.GetByWorkspaceAndEmailAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Participant?)null);
        _participantRepo.Setup(r => r.CreateAsync(It.IsAny<Participant>()))
            .Callback<Participant>(p => created = p)
            .ReturnsAsync((Participant p) => p);

        await CreateService().JoinAsync("token", new JoinRequest("  Mixed.Case@Example.COM "));

        Assert.Equal("mixed.case@example.com", created!.Email);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    private static CreateInviteRequest MakeCreateRequest(
        int maxUses = 10, DateTime? expiresAt = null, Guid? templateWorkspaceId = null) =>
        new(maxUses, expiresAt ?? DateTime.UtcNow.AddDays(30), templateWorkspaceId);

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsInviteWithRandomToken()
    {
        _inviteRepo.Setup(r => r.CreateAsync(It.IsAny<Invite>())).ReturnsAsync((Invite i) => i);
        var workspaceId = Guid.NewGuid();

        var result = await CreateService().CreateAsync(MakeCreateRequest(), workspaceId);

        Assert.Equal(workspaceId, result.WorkspaceId);
        Assert.Equal(10, result.MaxUses);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.DoesNotContain('+', result.Token);
        Assert.DoesNotContain('/', result.Token);
        Assert.DoesNotContain('=', result.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public async Task CreateAsync_MaxUsesOutOfRange_ThrowsValidationException(int maxUses)
    {
        // An invite is the only access control in the product; an unbounded MaxUses is a
        // permanent open door.
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(MakeCreateRequest(maxUses: maxUses), Guid.NewGuid()));

        _inviteRepo.Verify(r => r.CreateAsync(It.IsAny<Invite>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ExpiryInThePast_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(
                MakeCreateRequest(expiresAt: DateTime.UtcNow.AddSeconds(-1)), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_ExpiryBeyondMaxLifetime_ThrowsValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateService().CreateAsync(
                MakeCreateRequest(expiresAt: DateTime.UtcNow.AddYears(50)), Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_TemplateTheCallerWorksIn_IsAllowed()
    {
        var templateId = Guid.NewGuid();
        var template = new Workspace { Id = templateId, Name = "Starter", IsTemplate = true };

        _workspaceRepo.Setup(r => r.GetByIdAsync(templateId)).ReturnsAsync(template);
        _inviteRepo.Setup(r => r.CreateAsync(It.IsAny<Invite>())).ReturnsAsync((Invite i) => i);

        var result = await CreateService().CreateAsync(
            MakeCreateRequest(templateWorkspaceId: templateId), templateId);

        Assert.Equal(templateId, result.TemplateWorkspaceId);
    }

    [Fact]
    public async Task CreateAsync_TemplateTheCallersWorkspaceWasCopiedFrom_IsAllowed()
    {
        var templateId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        _workspaceRepo.Setup(r => r.GetByIdAsync(templateId))
            .ReturnsAsync(new Workspace { Id = templateId, IsTemplate = true });
        _workspaceRepo.Setup(r => r.GetByIdAsync(callerId))
            .ReturnsAsync(new Workspace { Id = callerId, SourceTemplateId = templateId });
        _inviteRepo.Setup(r => r.CreateAsync(It.IsAny<Invite>())).ReturnsAsync((Invite i) => i);

        var result = await CreateService().CreateAsync(
            MakeCreateRequest(templateWorkspaceId: templateId), callerId);

        Assert.Equal(templateId, result.TemplateWorkspaceId);
    }

    [Fact]
    public async Task CreateAsync_UnrelatedTemplate_ThrowsTemplateWorkspaceNotFound()
    {
        // Without this check any participant could point an invite at any workspace whose id
        // they guessed, provided it happened to be flagged as a template.
        var templateId = Guid.NewGuid();
        var callerId = Guid.NewGuid();

        _workspaceRepo.Setup(r => r.GetByIdAsync(templateId))
            .ReturnsAsync(new Workspace { Id = templateId, IsTemplate = true });
        _workspaceRepo.Setup(r => r.GetByIdAsync(callerId))
            .ReturnsAsync(new Workspace { Id = callerId, SourceTemplateId = null });

        await Assert.ThrowsAsync<TemplateWorkspaceNotFoundException>(() =>
            CreateService().CreateAsync(MakeCreateRequest(templateWorkspaceId: templateId), callerId));

        _inviteRepo.Verify(r => r.CreateAsync(It.IsAny<Invite>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_TargetIsNotATemplate_ThrowsTemplateWorkspaceNotFound()
    {
        var targetId = Guid.NewGuid();
        _workspaceRepo.Setup(r => r.GetByIdAsync(targetId))
            .ReturnsAsync(new Workspace { Id = targetId, IsTemplate = false });

        await Assert.ThrowsAsync<TemplateWorkspaceNotFoundException>(() =>
            CreateService().CreateAsync(MakeCreateRequest(templateWorkspaceId: targetId), targetId));
    }

    [Fact]
    public async Task CreateAsync_TemplateDoesNotExist_ThrowsTemplateWorkspaceNotFound()
    {
        var missingId = Guid.NewGuid();
        _workspaceRepo.Setup(r => r.GetByIdAsync(missingId)).ReturnsAsync((Workspace?)null);

        await Assert.ThrowsAsync<TemplateWorkspaceNotFoundException>(() =>
            CreateService().CreateAsync(MakeCreateRequest(templateWorkspaceId: missingId), Guid.NewGuid()));
    }
}
