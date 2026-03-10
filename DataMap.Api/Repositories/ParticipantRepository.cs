using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class ParticipantRepository(AppDbContext db) : IParticipantRepository
{
    public async Task<Participant?> GetByWorkspaceAndEmailAsync(Guid workspaceId, string email)
    {
        return await db.Participants
            .FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Email == email);
    }

    public async Task<Participant> CreateAsync(Participant participant)
    {
        db.Participants.Add(participant);
        await db.SaveChangesAsync();
        return participant;
    }

    public async Task UpdateLastSeenAtAsync(Guid participantId, DateTime lastSeenAt)
    {
        await db.Participants
            .Where(p => p.Id == participantId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.LastSeenAt, lastSeenAt));
    }
}
