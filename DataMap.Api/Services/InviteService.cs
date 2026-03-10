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

        var existingParticipant = await participantRepo.GetByWorkspaceAndEmailAsync(invite.WorkspaceId, request.Email);

        Participant participant;
        if (existingParticipant is not null)
        {
            await participantRepo.UpdateLastSeenAtAsync(existingParticipant.Id, DateTime.UtcNow);
            participant = existingParticipant;
        }
        else
        {
            var newParticipant = new Participant
            {
                Id = Guid.NewGuid(),
                WorkspaceId = invite.WorkspaceId,
                Email = request.Email,
                InviteId = invite.Id,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            participant = await participantRepo.CreateAsync(newParticipant);
            await inviteRepo.IncrementUsedCountAsync(invite.Id);
        }

        var session = new ParticipantSession
        {
            Id = Guid.NewGuid(),
            ParticipantId = participant.Id,
            WorkspaceId = invite.WorkspaceId,
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
            participant.Id, invite.WorkspaceId);

        return new JoinResponse(
            participant.Id,
            invite.WorkspaceId,
            invite.Workspace.Name,
            participant.Email);
    }
}
