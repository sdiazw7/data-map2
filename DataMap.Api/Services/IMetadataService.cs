using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IMetadataService
{
    Task<PagedResult<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query);

    /// <summary>Applies the edits and returns each affected column's new version.</summary>
    Task<BulkUpdateResponse> BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates);

    Task<CoverageResponse> GetCoverageAsync(Guid workspaceId);
    Task<PagedResult<string>> GetTableNamesAsync(Guid workspaceId, PageQuery page);
}
