using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IBusinessTermRepository
{
    Task<(List<BusinessTerm> Terms, int Total)> GetAllAsync(Guid workspaceId, int limit, int offset);
    Task<BusinessTerm?> GetByIdAsync(Guid termId);
    Task<BusinessTerm?> GetByNameAsync(Guid workspaceId, string name);
    Task<BusinessTerm> CreateAsync(BusinessTerm term);
}
