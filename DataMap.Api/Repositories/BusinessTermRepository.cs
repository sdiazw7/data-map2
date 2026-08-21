using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class BusinessTermRepository(AppDbContext db) : IBusinessTermRepository
{
    public async Task<List<BusinessTerm>> GetAllAsync(Guid workspaceId)
    {
        return await db.BusinessTerms
            .Where(t => t.WorkspaceId == workspaceId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<BusinessTerm?> GetByIdAsync(Guid termId)
    {
        return await db.BusinessTerms.FirstOrDefaultAsync(t => t.Id == termId);
    }

    public async Task<BusinessTerm?> GetByNameAsync(Guid workspaceId, string name)
    {
        return await db.BusinessTerms
            .FirstOrDefaultAsync(t => t.WorkspaceId == workspaceId && t.Name == name);
    }

    public async Task<BusinessTerm> CreateAsync(BusinessTerm term)
    {
        db.BusinessTerms.Add(term);
        await db.SaveChangesAsync();
        return term;
    }
}
