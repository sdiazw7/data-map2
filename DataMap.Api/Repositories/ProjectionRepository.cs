using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class ProjectionRepository(AppDbContext db) : IProjectionRepository
{
    public async Task<List<ColumnCatalogEditor>> QueryAsync(
        Guid workspaceId, int limit, int offset, string? search, bool undocumentedOnly)
    {
        var query = db.ColumnCatalogEditor.Where(c => c.WorkspaceId == workspaceId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                EF.Functions.ILike(c.SchemaName, $"%{search}%") ||
                EF.Functions.ILike(c.TableName, $"%{search}%") ||
                EF.Functions.ILike(c.ColumnName, $"%{search}%"));

        if (undocumentedOnly)
            query = query.Where(c => c.Description == null || c.Description == string.Empty);

        return await query.Skip(offset).Take(limit).ToListAsync();
    }

    public async Task RefreshAsync(Guid workspaceId)
    {
        await db.ColumnCatalogEditor
            .Where(c => c.WorkspaceId == workspaceId)
            .ExecuteDeleteAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO column_catalog_editor
                (workspace_id, column_id, schema_name, table_name, column_name, data_type, example_value, description, business_term, owner, version)
            SELECT
                col.workspace_id,
                col.id,
                s.name,
                t.name,
                col.name,
                col.data_type,
                col.example_value,
                col.description,
                bt.name,
                col.owner,
                col.version
            FROM columns col
            JOIN tables t ON t.id = col.table_id
            JOIN schemas s ON s.id = t.schema_id
            LEFT JOIN term_column_mappings tcm ON tcm.column_id = col.id
            LEFT JOIN business_terms bt ON bt.id = tcm.term_id
            WHERE col.workspace_id = {0}",
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
