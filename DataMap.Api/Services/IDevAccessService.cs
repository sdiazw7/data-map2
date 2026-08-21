using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IDevAccessService
{
    Task<List<WorkspaceSummaryDto>> ListWorkspacesAsync();
    Task<JoinResult> JoinAsync(Guid workspaceId);
}
