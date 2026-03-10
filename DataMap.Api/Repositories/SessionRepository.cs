using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class SessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task<ParticipantSession?> GetByIdAsync(Guid sessionId)
    {
        return await db.ParticipantSessions.FindAsync(sessionId);
    }

    public async Task<ParticipantSession> CreateAsync(ParticipantSession session)
    {
        db.ParticipantSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task UpdateLastSeenAtAsync(Guid sessionId, DateTime lastSeenAt)
    {
        await db.ParticipantSessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastSeenAt, lastSeenAt));
    }
}
