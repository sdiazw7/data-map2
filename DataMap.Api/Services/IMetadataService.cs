using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IMetadataService
{
    Task<List<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query);
    Task BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates);
    Task<CoverageResponse> GetCoverageAsync(Guid workspaceId);
    Task<List<string>> GetTableNamesAsync(Guid workspaceId);
}
