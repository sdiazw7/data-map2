using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class SchemaRepository(AppDbContext db) : ISchemaRepository
{
    public async Task<Schema?> GetByNameAsync(Guid workspaceId, string name)
    {
        return await db.Schemas
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId && s.Name == name);
    }

    public async Task<Schema> UpsertAsync(Guid workspaceId, string name)
    {
        var existing = await GetByNameAsync(workspaceId, name);
        if (existing is not null) return existing;

        var schema = new Schema { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name };
        db.Schemas.Add(schema);
        await db.SaveChangesAsync();
        return schema;
    }

    public async Task<IReadOnlyDictionary<string, Guid>> UpsertManyAsync(
        Guid workspaceId, IReadOnlyCollection<string> names)
    {
        // Ordinal comparison throughout: the (WorkspaceId, Name) unique index is
        // case-sensitive, so anything looser would treat two rows the database keeps
        // apart as one and hand back the wrong id.
        var byName = await db.Schemas
            .Where(s => s.WorkspaceId == workspaceId)
            .ToDictionaryAsync(s => s.Name, s => s, StringComparer.Ordinal);

        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            if (byName.ContainsKey(name)) continue;

            var schema = new Schema { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name };
            db.Schemas.Add(schema);
            byName[name] = schema;
        }

        await db.SaveChangesAsync();
        return byName.ToDictionary(kv => kv.Key, kv => kv.Value.Id, StringComparer.Ordinal);
    }

    public async Task<List<Schema>> GetAllByWorkspaceAsync(Guid workspaceId)
    {
        return await db.Schemas
            .Where(s => s.WorkspaceId == workspaceId)
            .ToListAsync();
    }
}
