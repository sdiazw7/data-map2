using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class BusinessTermService(
    IBusinessTermRepository termRepo,
    IColumnRepository columnRepo,
    IProjectionService projectionService,
    IUnitOfWork unitOfWork,
    ILogger<BusinessTermService> logger) : BaseService(logger), IBusinessTermService
{
    private const int MaxLimit = 1_000;
    private const int MaxNameLength = 200;
    private const int MaxDefinitionLength = 4_000;

    public async Task<PagedResult<BusinessTermDto>> GetAllAsync(Guid workspaceId, PageQuery page)
    {
        RequirePaging(page.Limit, page.Offset, MaxLimit);

        var (terms, total) = await termRepo.GetAllAsync(workspaceId, page.Limit, page.Offset);

        var items = terms.Select(t => new BusinessTermDto(t.Id, t.Name, t.Definition)).ToList();
        return new PagedResult<BusinessTermDto>(items, total, page.Limit, page.Offset);
    }

    public async Task<BusinessTermDto> GetByIdAsync(Guid workspaceId, Guid termId)
    {
        var term = await termRepo.GetByIdAsync(termId);

        // Scoped to the caller's workspace, so a term id from another workspace reads as
        // absent rather than confirming it exists.
        if (term is null || term.WorkspaceId != workspaceId)
            throw new BusinessTermNotFoundException();

        return new BusinessTermDto(term.Id, term.Name, term.Definition);
    }

    public async Task<BusinessTermDto> CreateAsync(Guid workspaceId, BusinessTermCreateRequest request)
    {
        var name = RequireText(request.Name, "Term name", MaxNameLength);
        var definition = OptionalText(request.Definition, "Definition", MaxDefinitionLength) ?? string.Empty;

        // (workspace_id, name) is uniquely indexed. Checking first turns a retyped term into a
        // 409 the UI can explain, instead of a DbUpdateException surfacing as a 500.
        var existing = await termRepo.GetByNameAsync(workspaceId, name);
        if (existing is not null)
            throw new BusinessTermAlreadyExistsException(name);

        var created = await termRepo.CreateAsync(new BusinessTerm
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = name,
            Definition = definition
        });

        Logger.LogInformation(
            "Business term {TermId} created in workspace {WorkspaceId}",
            created.Id, workspaceId);

        return new BusinessTermDto(created.Id, created.Name, created.Definition);
    }

    public async Task MapToColumnAsync(Guid workspaceId, Guid columnId, Guid termId)
    {
        var term = await termRepo.GetByIdAsync(termId);
        if (term is null || term.WorkspaceId != workspaceId)
            throw new BusinessTermNotFoundException();

        await SetTermAsync(workspaceId, columnId, termId, term.Name);

        Logger.LogInformation(
            "Term {TermId} mapped to column {ColumnId} in workspace {WorkspaceId}",
            termId, columnId, workspaceId);
    }

    public async Task UnmapFromColumnAsync(Guid workspaceId, Guid columnId)
    {
        await SetTermAsync(workspaceId, columnId, null, null);

        Logger.LogInformation(
            "Term cleared from column {ColumnId} in workspace {WorkspaceId}",
            columnId, workspaceId);
    }

    private async Task SetTermAsync(Guid workspaceId, Guid columnId, Guid? termId, string? termName)
    {
        // Scope the column to the caller's workspace. Without this a participant could map
        // their term onto another workspace's column and corrupt that workspace's projection.
        var column = await columnRepo.GetByIdAsync(workspaceId, columnId);
        if (column is null)
            throw new ColumnNotFoundException();

        await unitOfWork.ExecuteAsync(async () =>
        {
            // A column holds at most one term, so this always replaces whatever was mapped before.
            await columnRepo.SetBusinessTermAsync(workspaceId, columnId, termId);

            // The column and the projection row it feeds must land together, or the grid shows
            // a term the catalog does not have (or misses one it does).
            await projectionService.SyncColumnTermAsync(workspaceId, columnId, termName);
        });
    }
}
