using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IBusinessTermService
{
    Task<PagedResult<BusinessTermDto>> GetAllAsync(Guid workspaceId, PageQuery page);
    Task<BusinessTermDto> GetByIdAsync(Guid workspaceId, Guid termId);
    Task<BusinessTermDto> CreateAsync(Guid workspaceId, BusinessTermCreateRequest request);

    /// <summary>
    /// Assigns a term to a column, replacing whatever was mapped before, and returns the
    /// column's new version. The mapping moves the row's concurrency token, so a caller that
    /// did not take the new value would have its next edit to that row rejected as stale.
    /// </summary>
    Task<ColumnVersionDto> MapToColumnAsync(Guid workspaceId, Guid participantId, Guid columnId, Guid termId);

    /// <summary>Clears a column's term. Succeeds whether or not one was set.</summary>
    Task<ColumnVersionDto> UnmapFromColumnAsync(Guid workspaceId, Guid participantId, Guid columnId);
}
