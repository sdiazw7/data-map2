using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IMetadataChangeRepository
{
    Task AddRangeAsync(IEnumerable<MetadataChange> changes);

    /// <summary>
    /// One column's recorded edits, newest first, with the participant who made each. Takes the
    /// column id alone: the caller has already established that the column is in its workspace,
    /// and a change record carries no workspace of its own to filter on.
    /// </summary>
    Task<(List<MetadataChange> Changes, int Total)> GetByColumnAsync(Guid columnId, int limit, int offset);
}
