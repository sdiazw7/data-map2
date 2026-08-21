using System.Security.Cryptography;
using DataMap.Api.Data;
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
    IUnitOfWork unitOfWork,
    ILogger<InviteService> logger) : BaseService(logger), IInviteService
{
    private const int MaxEmailLength = 320;

    // An invite is the only access control in the product, so neither its reach nor its lifetime
    // is left open — an unbounded MaxUses with a distant expiry is a permanent open door.
    private const int MaxInviteUses = 1_000;
    private static readonly TimeSpan MaxInviteLifetime = TimeSpan.FromDays(365);

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

    public async Task<JoinResult> JoinAsync(string token, JoinRequest request)
    {
        var invite = await inviteRepo.GetByTokenAsync(token);
        if (invite is null)
            throw new InviteNotFoundException();

        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new InviteExpiredException();

        if (invite.UsedCount >= invite.MaxUses)
            throw new InviteUsageExceededException();

        var email = NormalizeEmail(request.Email);

        // One transaction across the whole join. A template join copies an entire workspace
        // before it creates the participant, and the copy is only ever found again by looking
        // up that participant — so a failure in between would strand the copy permanently and
        // hand the user a fresh one on every retry.
        var (participant, workspaceId, session) = await unitOfWork.ExecuteAsync(async () =>
        {
            var (joined, joinedWorkspaceId) = invite.TemplateWorkspaceId is not null
                ? await JoinTemplateInviteAsync(invite, email)
                : await JoinSharedInviteAsync(invite, email);

            var newSession = new ParticipantSession
            {
                Id = Guid.NewGuid(),
                ParticipantId = joined.Id,
                WorkspaceId = joinedWorkspaceId,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            await sessionRepo.CreateAsync(newSession);

            return (joined, joinedWorkspaceId, newSession);
        });

        Logger.LogInformation("Participant {ParticipantId} joined workspace {WorkspaceId}",
            participant.Id, workspaceId);

        return new JoinResult(
            new JoinResponse(
                participant.Id,
                workspaceId,
                invite.Workspace.Name,
                participant.Email),
            session.Id);
    }

    public async Task<CreateInviteResponse> CreateAsync(CreateInviteRequest request, Guid workspaceId)
    {
        Require(request.MaxUses >= 1, "MaxUses must be at least 1.");
        Require(request.MaxUses <= MaxInviteUses, $"MaxUses must be at most {MaxInviteUses:N0}.");
        Require(request.ExpiresAt > DateTime.UtcNow, "ExpiresAt must be in the future.");
        Require(request.ExpiresAt <= DateTime.UtcNow.Add(MaxInviteLifetime),
            $"ExpiresAt must be within {MaxInviteLifetime.TotalDays:N0} days.");

        if (request.TemplateWorkspaceId is not null)
            await AuthorizeTemplateAsync(request.TemplateWorkspaceId.Value, workspaceId);

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

    /// <summary>
    /// A caller may only build a template invite around a template they are actually working in —
    /// the template itself, or a copy made from it.
    /// </summary>
    private async Task AuthorizeTemplateAsync(Guid templateWorkspaceId, Guid callerWorkspaceId)
    {
        var template = await workspaceRepo.GetByIdAsync(templateWorkspaceId);
        var caller = await workspaceRepo.GetByIdAsync(callerWorkspaceId);

        var authorized = template is { IsTemplate: true }
            && caller is not null
            && (caller.Id == template.Id || caller.SourceTemplateId == template.Id);

        if (!authorized)
        {
            Logger.LogWarning(
                "Workspace {WorkspaceId} was refused a template invite for {TemplateWorkspaceId}",
                callerWorkspaceId, templateWorkspaceId);

            // Reported as not-found rather than forbidden so the response cannot be used to
            // confirm which guessed ids happen to be real template workspaces.
            throw new TemplateWorkspaceNotFoundException();
        }
    }

    /// <summary>
    /// Trims and lowercases the address. Participants are keyed by <c>(workspace_id, email)</c>
    /// on a case-sensitive unique index, so an un-normalized address would let one person hold
    /// two participant rows — and on a template invite, two separate private copies of the
    /// workspace, with their work split across both.
    /// </summary>
    private static string NormalizeEmail(string? email)
    {
        var trimmed = RequireText(email, "Email", MaxEmailLength).ToLowerInvariant();

        var at = trimmed.IndexOf('@');
        Require(
            at > 0 && at < trimmed.Length - 1 && trimmed.IndexOf('@', at + 1) < 0,
            "Email must be a valid address.");

        return trimmed;
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
