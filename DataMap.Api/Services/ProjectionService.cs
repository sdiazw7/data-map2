using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class ProjectionService(
    IProjectionRepository projectionRepo,
    ILogger<ProjectionService> logger) : BaseService(logger), IProjectionService
{
    public async Task RefreshAsync(Guid workspaceId)
    {
        Logger.LogInformation("Refreshing projection for workspace {WorkspaceId}", workspaceId);
        await projectionRepo.RefreshAsync(workspaceId);
        Logger.LogInformation("Projection refresh complete for workspace {WorkspaceId}", workspaceId);
    }
}
