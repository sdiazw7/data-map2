using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IDevAccessService
{
    Task<PagedResult<WorkspaceSummaryDto>> ListWorkspacesAsync(PageQuery page);
    Task<JoinResult> JoinAsync(Guid workspaceId);
}
