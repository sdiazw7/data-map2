using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

/// <summary>One CSV row's worth of column metadata, already validated and trimmed.</summary>
public record ColumnImport(Guid TableId, string Name, string DataType);

/// <param name="Conflicted">
/// True when another writer changed one of the rows between the read and the write.
/// The caller decides what that means; the repository does not throw for it.
/// </param>
public record ColumnUpsertResult(int Created, int Updated, bool Conflicted);

public interface IColumnRepository
{
    Task<Column?> GetByIdAsync(Guid workspaceId, Guid columnId);
    Task<List<Column>> GetByIdsAsync(Guid workspaceId, IReadOnlyCollection<Guid> columnIds);
    Task<Column> UpsertAsync(Guid workspaceId, Guid tableId, string name, string dataType);

    /// <summary>Upserts a whole batch keyed by (table, name) using one read and one write.</summary>
    Task<ColumnUpsertResult> UpsertManyAsync(Guid workspaceId, IReadOnlyCollection<ColumnImport> columns);

    /// <summary>
    /// Persists edits to the given columns in a single write. Returns false when the
    /// optimistic-concurrency check on Version rejected one of them.
    /// </summary>
    Task<bool> UpdateRangeAsync(IReadOnlyCollection<Column> columns);

    Task<List<Column>> GetAllByWorkspaceAsync(Guid workspaceId);

    /// <summary>
    /// Sets the column's business term and bumps its Version, which the caller reads back off
    /// the entity. Returns false when the optimistic-concurrency check on Version rejected the
    /// write. Takes the loaded column rather than an id: the caller has already read it to
    /// find the term it is replacing, and that read is what the concurrency check compares to.
    /// </summary>
    Task<bool> SetBusinessTermAsync(Column column, Guid? businessTermId);
}
