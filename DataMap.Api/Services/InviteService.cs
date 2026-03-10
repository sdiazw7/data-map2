using System.Security.Cryptography;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class InviteService(
    IInviteRepository inviteRepo,
    IParticipantRepository participantRepo,
    ISessionRepository sessionRepo,
    IWorkspaceRepository workspaceRepo,
    IWorkspaceCopyService workspaceCopyService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<InviteService> logger) : BaseService(logger), IInviteService
{
    public async Task<InviteDto> GetAsync(string token)
    {
        var invite = await inviteRepo.GetByTokenAsync(token);
        if (invite is null)
            throw new InviteNotFoundException();

        var isValid = invite.ExpiresAt > DateTime.UtcNow && invite.UsedCount < invite.MaxUses;

        return new InviteDto(
            invite.Id,
            invite.WorkspaceId,
            invite.Workspace.Name,
            invite.ExpiresAt,
            isValid);
    }

    public async Task<JoinResponse> JoinAsync(string token, JoinRequest request)
    {
        var invite = await inviteRepo.GetByTokenAsync(token);
        if (invite is null)
            throw new InviteNotFoundException();

        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new InviteExpiredException();

        if (invite.UsedCount >= invite.MaxUses)
            throw new InviteUsageExceededException();

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ValidationException("Email is required.");

        Participant participant;
        Guid workspaceId;

        if (invite.TemplateWorkspaceId is not null)
        {
            (participant, workspaceId) = await JoinTemplateInviteAsync(invite, request.Email);
        }
        else
        {
            (participant, workspaceId) = await JoinSharedInviteAsync(invite, request.Email);
        }

        var session = new ParticipantSession
        {
            Id = Guid.NewGuid(),
            ParticipantId = participant.Id,
            WorkspaceId = workspaceId,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        };
        await sessionRepo.CreateAsync(session);

        var httpContext = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context.");

        httpContext.Response.Cookies.Append("participant_session", session.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(30),
        });

        Logger.LogInformation("Participant {ParticipantId} joined workspace {WorkspaceId}",
            participant.Id, workspaceId);

        return new JoinResponse(
            participant.Id,
            workspaceId,
            invite.Workspace.Name,
            participant.Email);
    }

    public async Task<CreateInviteResponse> CreateAsync(CreateInviteRequest request, Guid workspaceId)
    {
        if (request.MaxUses < 1)
            throw new ValidationException("MaxUses must be at least 1.");

        if (request.ExpiresAt <= DateTime.UtcNow)
            throw new ValidationException("ExpiresAt must be in the future.");

        if (request.TemplateWorkspaceId is not null)
        {
            var template = await workspaceRepo.GetByIdAsync(request.TemplateWorkspaceId.Value);
            if (template is null || !template.IsTemplate)
                throw new TemplateWorkspaceNotFoundException();
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt,
            MaxUses = request.MaxUses,
            UsedCount = 0,
            TemplateWorkspaceId = request.TemplateWorkspaceId,
        };
        await inviteRepo.CreateAsync(invite);

        Logger.LogInformation("Invite {InviteId} created for workspace {WorkspaceId}", invite.Id, workspaceId);

        return new CreateInviteResponse(
            invite.Id,
            invite.Token,
            invite.WorkspaceId,
            invite.ExpiresAt,
            invite.MaxUses,
            invite.TemplateWorkspaceId);
    }

    private async Task<(Participant, Guid WorkspaceId)> JoinSharedInviteAsync(Invite invite, string email)
    {
        var existing = await participantRepo.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, email);
        if (existing is not null)
        {
            await participantRepo.UpdateLastSeenAtAsync(existing.Id, DateTime.UtcNow);
            return (existing, invite.WorkspaceId);
        }

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            WorkspaceId = invite.WorkspaceId,
            Email = email,
            InviteId = invite.Id,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        };
        await participantRepo.CreateAsync(participant);
        await inviteRepo.IncrementUsedCountAsync(invite.Id);
        return (participant, invite.WorkspaceId);
    }

    private async Task<(Participant, Guid WorkspaceId)> JoinTemplateInviteAsync(Invite invite, string email)
    {
        var templateWorkspaceId = invite.TemplateWorkspaceId!.Value;

        // Returning user: find their existing workspace copy
        var existingWorkspace = await workspaceRepo.FindBySourceTemplateAndEmailAsync(templateWorkspaceId, email);
        if (existingWorkspace is not null)
        {
            var existingParticipant = await participantRepo.GetByWorkspaceAndEmailAsync(existingWorkspace.Id, email);
            await participantRepo.UpdateLastSeenAtAsync(existingParticipant!.Id, DateTime.UtcNow);
            return (existingParticipant, existingWorkspace.Id);
        }

        // New user: create a fresh copy of the template workspace
        var newWorkspace = await workspaceCopyService.CopyAsync(templateWorkspaceId, invite.Workspace.Name);

        var participant = new Participant
        {
            Id = Guid.NewGuid(),
            WorkspaceId = newWorkspace.Id,
            Email = email,
            InviteId = invite.Id,
            CreatedAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow,
        };
        await participantRepo.CreateAsync(participant);
        await inviteRepo.IncrementUsedCountAsync(invite.Id);
        return (participant, newWorkspace.Id);
    }
}
