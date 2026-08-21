using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface ISchemaRepository
{
    Task<Schema?> GetByNameAsync(Guid workspaceId, string name);
    Task<Schema> UpsertAsync(Guid workspaceId, string name);

    /// <summary>Upserts a batch of schema names using one read and one write. Returns name to id.</summary>
    Task<IReadOnlyDictionary<string, Guid>> UpsertManyAsync(Guid workspaceId, IReadOnlyCollection<string> names);

    Task<List<Schema>> GetAllByWorkspaceAsync(Guid workspaceId);
}
