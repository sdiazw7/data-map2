namespace DataMap.Api.Services;

public interface IProjectionService
{
    Task RefreshAsync(Guid workspaceId);
}
