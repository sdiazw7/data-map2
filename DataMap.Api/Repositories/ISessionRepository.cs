using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface ISessionRepository
{
    Task<ParticipantSession?> GetByIdAsync(Guid sessionId);
    Task<ParticipantSession> CreateAsync(ParticipantSession session);
    Task UpdateLastSeenAtAsync(Guid sessionId, DateTime lastSeenAt);
}
