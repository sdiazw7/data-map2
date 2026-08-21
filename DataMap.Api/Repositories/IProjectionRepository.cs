using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IProjectionRepository
{
    Task<List<ColumnCatalogEditor>> QueryAsync(Guid workspaceId, int limit, int offset, string? search, bool undocumentedOnly, string? tableName, string sortBy, string sortDir);

    /// <summary>Rebuilds the whole workspace projection. For bulk structural changes only.</summary>
    Task RefreshAsync(Guid workspaceId);

    /// <summary>Updates the projection rows for the given edited columns in place.</summary>
    Task SyncColumnsAsync(Guid workspaceId, IReadOnlyCollection<Column> columns);

    /// <summary>Updates the business term on a single column's projection row.</summary>
    Task SyncColumnTermAsync(Guid workspaceId, Guid columnId, string? termName);

    Task<(int Total, int Documented)> GetCoverageCountsAsync(Guid workspaceId);
    Task<List<string>> GetDistinctTableNamesAsync(Guid workspaceId);
}
