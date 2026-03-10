using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IColumnRepository
{
    Task<Column?> GetByIdAsync(Guid workspaceId, Guid columnId);
    Task<Column> UpsertAsync(Guid workspaceId, Guid tableId, string name, string dataType);
    Task<bool> UpdateAsync(Column column);
    Task<List<Column>> GetAllByWorkspaceAsync(Guid workspaceId);
}
