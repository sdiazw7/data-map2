using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class InviteRepository(AppDbContext db) : IInviteRepository
{
    public async Task<Invite?> GetByTokenAsync(string token)
    {
        return await db.Invites
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i => i.Token == token);
    }

    public async Task IncrementUsedCountAsync(Guid inviteId)
    {
        await db.Invites
            .Where(i => i.Id == inviteId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsedCount, i => i.UsedCount + 1));
    }
}
