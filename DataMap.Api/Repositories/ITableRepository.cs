using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface ITableRepository
{
    Task<Table?> GetByNameAsync(Guid workspaceId, Guid schemaId, string name);
    Task<Table> UpsertAsync(Guid workspaceId, Guid schemaId, string name);
}
