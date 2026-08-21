using DataMap.Api.Models;

namespace DataMap.Api.Services;

public interface IProjectionService
{
    /// <summary>
    /// Rebuilds the whole workspace projection. Use for bulk structural changes
    /// (CSV upload, workspace copy, seeding) — never for individual edits.
    /// </summary>
    Task RefreshAsync(Guid workspaceId);

    /// <summary>Propagates edits to the projection rows of the given columns.</summary>
    Task SyncColumnsAsync(Guid workspaceId, IReadOnlyCollection<Column> columns);

    /// <summary>Propagates a business term change to a single column's projection row.</summary>
    Task SyncColumnTermAsync(Guid workspaceId, Guid columnId, string? termName);
}
