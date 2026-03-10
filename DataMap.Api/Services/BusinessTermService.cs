using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class BusinessTermService(
    IBusinessTermRepository termRepo,
    IProjectionService projectionService,
    ILogger<BusinessTermService> logger) : BaseService(logger), IBusinessTermService
{
    public async Task<List<BusinessTermDto>> GetAllAsync(Guid workspaceId)
    {
        var terms = await termRepo.GetAllAsync(workspaceId);
        return terms.Select(t => new BusinessTermDto(t.Id, t.Name, t.Definition)).ToList();
    }

    public async Task<BusinessTermDto> CreateAsync(Guid workspaceId, BusinessTermCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Term name is required.");

        var term = new BusinessTerm
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Definition = request.Definition?.Trim() ?? string.Empty
        };

        var created = await termRepo.CreateAsync(term);

        Logger.LogInformation(
            "Business term {TermId} created in workspace {WorkspaceId}",
            created.Id, workspaceId);

        return new BusinessTermDto(created.Id, created.Name, created.Definition);
    }

    public async Task MapToColumnAsync(Guid workspaceId, TermMappingRequest request)
    {
        var term = await termRepo.GetByIdAsync(request.TermId);
        if (term is null || term.WorkspaceId != workspaceId)
            throw new ValidationException("Business term not found.");

        var mapping = new TermColumnMapping
        {
            Id = Guid.NewGuid(),
            TermId = request.TermId,
            ColumnId = request.ColumnId
        };

        await termRepo.MapTermToColumnAsync(mapping);

        Logger.LogInformation(
            "Term {TermId} mapped to column {ColumnId} in workspace {WorkspaceId}",
            request.TermId, request.ColumnId, workspaceId);

        await projectionService.RefreshAsync(workspaceId);
    }
}
