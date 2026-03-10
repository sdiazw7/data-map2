using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface ISchemaRepository
{
    Task<Schema?> GetByNameAsync(Guid workspaceId, string name);
    Task<Schema> UpsertAsync(Guid workspaceId, string name);
    Task<List<Schema>> GetAllByWorkspaceAsync(Guid workspaceId);
}
