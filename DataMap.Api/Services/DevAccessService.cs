using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

// Dev-only convenience login that skips the invite flow. Only reachable when
// ASPNETCORE_ENVIRONMENT is Development — see Program.cs and SessionAuthMiddleware.
public class DevAccessService(
    IWorkspaceRepository workspaceRepo,
    IInviteRepository inviteRepo,
    IParticipantRepository participantRepo,
    ISessionRepository sessionRepo,
    IUnitOfWork unitOfWork,
    ILogger<DevAccessService> logger) : BaseService(logger), IDevAccessService
{
    private const int MaxLimit = 500;
    private const string DevEmail = "dev@local";

    public async Task<PagedResult<WorkspaceSummaryDto>> ListWorkspacesAsync(PageQuery page)
    {
        RequirePaging(page.Limit, page.Offset, MaxLimit);

        var (workspaces, total) = await workspaceRepo.GetAllAsync(page.Limit, page.Offset);

        var items = workspaces.Select(w => new WorkspaceSummaryDto(w.Id, w.Name)).ToList();
        return new PagedResult<WorkspaceSummaryDto>(items, total, page.Limit, page.Offset);
    }

    public async Task<JoinResult> JoinAsync(Guid workspaceId)
    {
        var workspace = await workspaceRepo.GetByIdAsync(workspaceId);
        if (workspace is null)
            throw new WorkspaceNotFoundException();

        var session = await unitOfWork.ExecuteAsync(async () =>
        {
            // Participant.InviteId is a required FK, so dev access mints (or reuses) a
            // standing per-workspace invite behind the scenes rather than relaxing the schema.
            var devInviteToken = $"dev-{workspaceId}";
            var invite = await inviteRepo.GetByTokenAsync(devInviteToken);
            if (invite is null)
            {
                invite = new Invite
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    Token = devInviteToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(10),
                    MaxUses = int.MaxValue,
                    UsedCount = 0,
                    TemplateWorkspaceId = null,
                };
                await inviteRepo.CreateAsync(invite);
            }

            var participant = await participantRepo.GetByWorkspaceAndEmailAsync(workspaceId, DevEmail);
            if (participant is null)
            {
                participant = new Participant
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    Email = DevEmail,
                    InviteId = invite.Id,
                    CreatedAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow,
                };
                await participantRepo.CreateAsync(participant);
            }
            else
            {
                await participantRepo.UpdateLastSeenAtAsync(participant.Id, DateTime.UtcNow);
            }

            var newSession = new ParticipantSession
            {
                Id = Guid.NewGuid(),
                ParticipantId = participant.Id,
                WorkspaceId = workspaceId,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            await sessionRepo.CreateAsync(newSession);

            return (Participant: participant, Session: newSession);
        });

        Logger.LogInformation("Dev participant {ParticipantId} joined workspace {WorkspaceId}",
            session.Participant.Id, workspaceId);

        return new JoinResult(
            new JoinResponse(
                session.Participant.Id,
                workspaceId,
                workspace.Name,
                session.Participant.Email),
            session.Session.Id);
    }
}
