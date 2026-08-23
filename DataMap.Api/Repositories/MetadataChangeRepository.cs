using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class MetadataChangeRepository(AppDbContext db) : IMetadataChangeRepository
{
    public async Task AddRangeAsync(IEnumerable<MetadataChange> changes)
    {
        db.MetadataChanges.AddRange(changes);
        await db.SaveChangesAsync();
    }

    public async Task<(List<MetadataChange> Changes, int Total)> GetByColumnAsync(
        Guid columnId, int limit, int offset)
    {
        var query = db.MetadataChanges
            .Where(m => m.EntityType == "Column" && m.EntityId == columnId);

        var total = await query.CountAsync();

        var changes = await query
            // Newest first, since the last thing that happened to a cell is what a reader is
            // usually checking. Id breaks the tie: a batch writes every row's records with one
            // timestamp, so EditedAt alone does not order them stably across pages.
            .OrderByDescending(m => m.EditedAt)
            .ThenBy(m => m.Id)
            .Skip(offset)
            .Take(limit)
            .Include(m => m.Participant)
            .ToListAsync();

        return (changes, total);
    }
}
