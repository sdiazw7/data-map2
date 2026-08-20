using DataMap.Api.DTOs;
using Microsoft.AspNetCore.Http;

namespace DataMap.Api.Services;

public interface IMetadataService
{
    Task<List<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query);
    Task UploadCsvAsync(Guid workspaceId, Guid participantId, IFormFile file);
    Task BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates);
    Task<CoverageResponse> GetCoverageAsync(Guid workspaceId);
    Task<List<string>> GetTableNamesAsync(Guid workspaceId);
}
