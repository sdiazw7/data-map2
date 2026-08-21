using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface ITableRepository
{
    Task<Table?> GetByNameAsync(Guid workspaceId, Guid schemaId, string name);
    Task<Table> UpsertAsync(Guid workspaceId, Guid schemaId, string name);

    /// <summary>Upserts a batch of tables using one read and one write. Returns key to id.</summary>
    Task<IReadOnlyDictionary<(Guid SchemaId, string Name), Guid>> UpsertManyAsync(
        Guid workspaceId, IReadOnlyCollection<(Guid SchemaId, string Name)> tables);

    Task<List<Table>> GetAllByWorkspaceAsync(Guid workspaceId);
}
