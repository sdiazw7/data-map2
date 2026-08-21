using DataMap.Api.Models;
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

    public async Task SyncColumnsAsync(Guid workspaceId, IReadOnlyCollection<Column> columns)
    {
        if (columns.Count == 0) return;

        await projectionRepo.SyncColumnsAsync(workspaceId, columns);
        Logger.LogInformation(
            "Synced {ColumnCount} projection rows for workspace {WorkspaceId}",
            columns.Count, workspaceId);
    }

    public async Task SyncColumnTermAsync(Guid workspaceId, Guid columnId, string? termName)
    {
        await projectionRepo.SyncColumnTermAsync(workspaceId, columnId, termName);
        Logger.LogInformation(
            "Synced business term on projection row for column {ColumnId} in workspace {WorkspaceId}",
            columnId, workspaceId);
    }
}
