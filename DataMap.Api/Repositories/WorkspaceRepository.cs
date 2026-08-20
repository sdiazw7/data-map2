using DataMap.Api.Data;
using DataMap.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DataMap.Api.Repositories;

public class WorkspaceRepository(AppDbContext db) : IWorkspaceRepository
{
    public async Task<Workspace> CreateAsync(Workspace workspace)
    {
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();
        return workspace;
    }

    public async Task<Workspace?> GetByIdAsync(Guid id)
    {
        return await db.Workspaces.FindAsync(id);
    }

    public async Task<Workspace?> FindBySourceTemplateAndEmailAsync(Guid templateWorkspaceId, string email)
    {
        return await db.Workspaces
            .Where(w => w.SourceTemplateId == templateWorkspaceId
                && db.Participants.Any(p => p.WorkspaceId == w.Id && p.Email == email))
            .FirstOrDefaultAsync();
    }

    public async Task<List<Workspace>> GetAllAsync()
    {
        return await db.Workspaces.OrderBy(w => w.Name).ToListAsync();
    }
}
