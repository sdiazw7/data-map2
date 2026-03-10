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
