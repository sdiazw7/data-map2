using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IParticipantRepository
{
    Task<Participant?> GetByWorkspaceAndEmailAsync(Guid workspaceId, string email);
    Task<Participant> CreateAsync(Participant participant);
    Task UpdateLastSeenAtAsync(Guid participantId, DateTime lastSeenAt);
}
