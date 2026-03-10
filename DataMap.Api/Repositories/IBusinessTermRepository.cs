using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IBusinessTermRepository
{
    Task<List<BusinessTerm>> GetAllAsync(Guid workspaceId);
    Task<BusinessTerm?> GetByIdAsync(Guid termId);
    Task<BusinessTerm> CreateAsync(BusinessTerm term);
    Task<TermColumnMapping> MapTermToColumnAsync(TermColumnMapping mapping);
}
