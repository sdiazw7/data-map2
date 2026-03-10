using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace> CreateAsync(Workspace workspace);
    Task<Workspace?> GetByIdAsync(Guid id);
    Task<Workspace?> FindBySourceTemplateAndEmailAsync(Guid templateWorkspaceId, string email);
}
