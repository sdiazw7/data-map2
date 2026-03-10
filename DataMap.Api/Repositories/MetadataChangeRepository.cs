using DataMap.Api.Data;
using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public class MetadataChangeRepository(AppDbContext db) : IMetadataChangeRepository
{
    public async Task AddRangeAsync(IEnumerable<MetadataChange> changes)
    {
        db.MetadataChanges.AddRange(changes);
        await db.SaveChangesAsync();
    }
}
