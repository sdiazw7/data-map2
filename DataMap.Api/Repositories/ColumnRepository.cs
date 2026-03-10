using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class ColumnRepository(AppDbContext db) : IColumnRepository
{
    public async Task<Column?> GetByIdAsync(Guid workspaceId, Guid columnId)
    {
        return await db.Columns
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Id == columnId);
    }

    public async Task<Column> UpsertAsync(Guid workspaceId, Guid tableId, string name, string dataType)
    {
        var existing = await db.Columns
            .FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.TableId == tableId && c.Name == name);
        if (existing is not null)
        {
            existing.DataType = dataType;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }

        var now = DateTime.UtcNow;
        var column = new Column
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            TableId = tableId,
            Name = name,
            DataType = dataType,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Columns.Add(column);
        await db.SaveChangesAsync();
        return column;
    }

    public async Task<bool> UpdateAsync(Column column)
    {
        db.Columns.Update(column);
        var affected = await db.SaveChangesAsync();
        return affected > 0;
    }
}
