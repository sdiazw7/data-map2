using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class ProjectionRepository(AppDbContext db) : IProjectionRepository
{
    public async Task<List<ColumnCatalogEditor>> QueryAsync(
        Guid workspaceId, int limit, int offset, string? search, bool undocumentedOnly, string? tableName, string sortBy, string sortDir)
    {
        var query = db.ColumnCatalogEditor.Where(c => c.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                EF.Functions.ILike(c.SchemaName, $"%{search}%") ||
                EF.Functions.ILike(c.TableName, $"%{search}%") ||
                EF.Functions.ILike(c.ColumnName, $"%{search}%"));

        if (undocumentedOnly)
            query = query.Where(c => c.Description == null || c.Description == string.Empty);

        if (!string.IsNullOrWhiteSpace(tableName))
            query = query.Where(c => c.TableName == tableName);

        var descending = sortDir == "desc";
        IOrderedQueryable<ColumnCatalogEditor> ordered = sortBy switch
        {
            "table_name" => descending ? query.OrderByDescending(c => c.TableName) : query.OrderBy(c => c.TableName),
            "data_type" => descending ? query.OrderByDescending(c => c.DataType) : query.OrderBy(c => c.DataType),
            "owner" => descending ? query.OrderByDescending(c => c.Owner) : query.OrderBy(c => c.Owner),
            _ => descending ? query.OrderByDescending(c => c.ColumnName) : query.OrderBy(c => c.ColumnName),
        };
        // Tie-breaker keeps offset pagination deterministic across pages when the sort column
        // repeats values. It follows the sort direction so the ORDER BY matches a forward or
        // backward walk of the composite index; a fixed-ascending tie-breaker on a descending
        // sort would force Postgres to sort the whole result set instead.
        query = descending ? ordered.ThenByDescending(c => c.ColumnId) : ordered.ThenBy(c => c.ColumnId);

        return await query.AsNoTracking().Skip(offset).Take(limit).ToListAsync();
    }

    public async Task<List<string>> GetDistinctTableNamesAsync(Guid workspaceId)
    {
        return await db.ColumnCatalogEditor
            .Where(c => c.WorkspaceId == workspaceId)
            .Select(c => c.TableName)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    public async Task SyncColumnsAsync(Guid workspaceId, IReadOnlyCollection<Column> columns)
    {
        if (columns.Count == 0) return;

        var columnIds = columns.Select(c => c.Id).ToList();
        var rows = await db.ColumnCatalogEditor
            .Where(r => r.WorkspaceId == workspaceId && columnIds.Contains(r.ColumnId))
            .ToListAsync();

        var columnsById = columns.ToDictionary(c => c.Id);
        foreach (var row in rows)
        {
            var column = columnsById[row.ColumnId];
            row.ExampleValue = column.ExampleValue;
            row.Description = column.Description;
            row.Owner = column.Owner;
            row.Version = column.Version;
        }

        await db.SaveChangesAsync();
    }

    public async Task SyncColumnTermAsync(Guid workspaceId, Guid columnId, string? termName)
    {
        await db.ColumnCatalogEditor
            .Where(r => r.WorkspaceId == workspaceId && r.ColumnId == columnId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.BusinessTerm, termName));
    }

    public async Task RefreshAsync(Guid workspaceId)
    {
        // Delete and reinsert must commit together. Without a transaction the delete is
        // visible on its own, so concurrent readers see an empty catalog mid-rebuild, and
        // a failed insert leaves the workspace's projection permanently empty.
        // A caller may already have opened one (CSV upload rebuilds inside its own unit of
        // work); nesting a second transaction would throw, so reuse the ambient one.
        if (db.Database.CurrentTransaction is not null)
        {
            await RebuildAsync(workspaceId);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();
        await RebuildAsync(workspaceId);
        await transaction.CommitAsync();
    }

    private async Task RebuildAsync(Guid workspaceId)
    {
        await db.ColumnCatalogEditor
            .Where(c => c.WorkspaceId == workspaceId)
            .ExecuteDeleteAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO app.""ColumnCatalogEditor""
                (""WorkspaceId"", ""ColumnId"", ""SchemaName"", ""TableName"", ""ColumnName"", ""DataType"", ""ExampleValue"", ""Description"", ""BusinessTerm"", ""Owner"", ""Version"")
            SELECT
                col.""WorkspaceId"",
                col.""Id"",
                s.""Name"",
                t.""Name"",
                col.""Name"",
                col.""DataType"",
                col.""ExampleValue"",
                col.""Description"",
                bt.""Name"",
                col.""Owner"",
                col.""Version""
            FROM app.""Columns"" col
            JOIN app.""Tables"" t ON t.""Id"" = col.""TableId""
            JOIN app.""Schemas"" s ON s.""Id"" = t.""SchemaId""
            LEFT JOIN app.""TermColumnMappings"" tcm ON tcm.""ColumnId"" = col.""Id""
            LEFT JOIN app.""BusinessTerms"" bt ON bt.""Id"" = tcm.""TermId""
            WHERE col.""WorkspaceId"" = {0}",
            workspaceId);
    }

    public async Task<(int Total, int Documented)> GetCoverageCountsAsync(Guid workspaceId)
    {
        var total = await db.ColumnCatalogEditor.CountAsync(c => c.WorkspaceId == workspaceId);
        var documented = await db.ColumnCatalogEditor
            .CountAsync(c => c.WorkspaceId == workspaceId && c.Description != null && c.Description != string.Empty);
        return (total, documented);
    }
}
