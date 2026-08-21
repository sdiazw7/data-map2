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

    public async Task<List<Column>> GetByIdsAsync(Guid workspaceId, IReadOnlyCollection<Guid> columnIds)
    {
        if (columnIds.Count == 0) return [];

        return await db.Columns
            .Where(c => c.WorkspaceId == workspaceId && columnIds.Contains(c.Id))
            .ToListAsync();
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

    public async Task<ColumnUpsertResult> UpsertManyAsync(Guid workspaceId, IReadOnlyCollection<ColumnImport> columns)
    {
        if (columns.Count == 0) return new ColumnUpsertResult(0, 0, false);

        // Read the affected tables' existing columns once, then diff in memory. Fetching per
        // row instead would put two round trips on every line of a 100k-row import.
        var tableIds = columns.Select(c => c.TableId).Distinct().ToList();
        var existing = await db.Columns
            .Where(c => c.WorkspaceId == workspaceId && tableIds.Contains(c.TableId))
            .ToDictionaryAsync(c => (c.TableId, c.Name));

        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;

        foreach (var import in columns)
        {
            var key = (import.TableId, import.Name);
            if (existing.TryGetValue(key, out var column))
            {
                // Only mark the row dirty on a real change. Version is a concurrency token, so
                // a no-op write would still race live grid edits for no reason.
                if (column.DataType == import.DataType) continue;

                column.DataType = import.DataType;
                column.UpdatedAt = now;
                updated++;
                continue;
            }

            var inserted = new Column
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                TableId = import.TableId,
                Name = import.Name,
                DataType = import.DataType,
                Version = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Columns.Add(inserted);
            // Track it here too, so a name repeated later in the same file updates this row
            // instead of inserting a duplicate that violates the (table, name) unique index.
            existing[key] = inserted;
            created++;
        }

        try
        {
            await db.SaveChangesAsync();
            return new ColumnUpsertResult(created, updated, false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ColumnUpsertResult(0, 0, true);
        }
    }

    public async Task<bool> UpdateRangeAsync(IReadOnlyCollection<Column> columns)
    {
        if (columns.Count == 0) return true;

        foreach (var column in columns)
        {
            // Columns read through this repository are already tracked, and the tracking entry
            // is what holds the original Version the concurrency check compares against.
            // Only a detached entity needs attaching.
            if (db.Entry(column).State == EntityState.Detached)
                db.Columns.Update(column);
        }

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<List<Column>> GetAllByWorkspaceAsync(Guid workspaceId)
    {
        return await db.Columns
            .Where(c => c.WorkspaceId == workspaceId)
            .ToListAsync();
    }

    public async Task SetBusinessTermAsync(Guid workspaceId, Guid columnId, Guid? businessTermId)
    {
        await db.Columns
            .Where(c => c.WorkspaceId == workspaceId && c.Id == columnId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.BusinessTermId, businessTermId));
    }
}
