using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IMetadataChangeRepository
{
    Task AddRangeAsync(IEnumerable<MetadataChange> changes);
}
