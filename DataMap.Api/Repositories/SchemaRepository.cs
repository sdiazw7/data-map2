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
}
