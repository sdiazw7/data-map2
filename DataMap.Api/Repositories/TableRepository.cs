using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class TableRepository(AppDbContext db) : ITableRepository
{
    public async Task<Table?> GetByNameAsync(Guid workspaceId, Guid schemaId, string name)
    {
        return await db.Tables
            .FirstOrDefaultAsync(t => t.WorkspaceId == workspaceId && t.SchemaId == schemaId && t.Name == name);
    }

    public async Task<Table> UpsertAsync(Guid workspaceId, Guid schemaId, string name)
    {
        var existing = await GetByNameAsync(workspaceId, schemaId, name);
        if (existing is not null) return existing;

        var now = DateTime.UtcNow;
        var table = new Table
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            SchemaId = schemaId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Tables.Add(table);
        await db.SaveChangesAsync();
        return table;
    }
}
