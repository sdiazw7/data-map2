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
    private const int MaxNameLength = 200;
    private const int MaxDefinitionLength = 4_000;

    public async Task<List<BusinessTermDto>> GetAllAsync(Guid workspaceId)
    {
        var terms = await termRepo.GetAllAsync(workspaceId);
        return terms.Select(t => new BusinessTermDto(t.Id, t.Name, t.Definition)).ToList();
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

    public async Task MapToColumnAsync(Guid workspaceId, TermMappingRequest request)
    {
        var term = await termRepo.GetByIdAsync(request.TermId);
        if (term is null || term.WorkspaceId != workspaceId)
            throw new BusinessTermNotFoundException();

        // Scope the column to the caller's workspace. Without this a participant could map
        // their term onto another workspace's column and corrupt that workspace's projection.
        var column = await columnRepo.GetByIdAsync(workspaceId, request.ColumnId);
        if (column is null)
            throw new ColumnNotFoundException();

        await unitOfWork.ExecuteAsync(async () =>
        {
            // A column holds at most one term, so remapping replaces the existing mapping.
            var existing = await termRepo.GetMappingByColumnAsync(request.ColumnId);
            if (existing is not null)
            {
                existing.TermId = request.TermId;
                await termRepo.UpdateMappingAsync(existing);
            }
            else
            {
                await termRepo.MapTermToColumnAsync(new TermColumnMapping
                {
                    Id = Guid.NewGuid(),
                    TermId = request.TermId,
                    ColumnId = request.ColumnId
                });
            }

            // The mapping and the projection row it feeds must land together, or the grid shows
            // a term the catalog does not have (or misses one it does).
            await projectionService.SyncColumnTermAsync(workspaceId, request.ColumnId, term.Name);
        });

        Logger.LogInformation(
            "Term {TermId} mapped to column {ColumnId} in workspace {WorkspaceId}",
            request.TermId, request.ColumnId, workspaceId);
    }
}
