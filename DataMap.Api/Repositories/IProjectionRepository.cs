using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IProjectionRepository
{
    Task<List<ColumnCatalogEditor>> QueryAsync(Guid workspaceId, int limit, int offset, string? search, bool undocumentedOnly, string? tableName, string sortBy, string sortDir);
    Task RefreshAsync(Guid workspaceId);
    Task<(int Total, int Documented)> GetCoverageCountsAsync(Guid workspaceId);
    Task<List<string>> GetDistinctTableNamesAsync(Guid workspaceId);
}
