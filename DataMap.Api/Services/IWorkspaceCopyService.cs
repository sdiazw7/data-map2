using DataMap.Api.Models;

namespace DataMap.Api.Services;

public interface IWorkspaceCopyService
{
    Task<Workspace> CopyAsync(Guid templateWorkspaceId, string workspaceName);
}
