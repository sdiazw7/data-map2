using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IBusinessTermService
{
    Task<PagedResult<BusinessTermDto>> GetAllAsync(Guid workspaceId, PageQuery page);
    Task<BusinessTermDto> GetByIdAsync(Guid workspaceId, Guid termId);
    Task<BusinessTermDto> CreateAsync(Guid workspaceId, BusinessTermCreateRequest request);

    /// <summary>Assigns a term to a column, replacing whatever was mapped before.</summary>
    Task MapToColumnAsync(Guid workspaceId, Guid columnId, Guid termId);

    /// <summary>Clears a column's term. Succeeds whether or not one was set.</summary>
    Task UnmapFromColumnAsync(Guid workspaceId, Guid columnId);
}
