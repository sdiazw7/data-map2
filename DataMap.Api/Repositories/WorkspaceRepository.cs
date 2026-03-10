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

    public async Task<Workspace?> FindBySourceTemplateAndEmailAsync(Guid templateWorkspaceId, string email)
    {
        return await db.Workspaces
            .Where(w => w.SourceTemplateId == templateWorkspaceId
                && db.Participants.Any(p => p.WorkspaceId == w.Id && p.Email == email))
            .FirstOrDefaultAsync();
    }
}
