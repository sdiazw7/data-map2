using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class WorkspaceCopyService(
    IWorkspaceRepository workspaceRepo,
    ISchemaRepository schemaRepo,
    ITableRepository tableRepo,
    IColumnRepository columnRepo,
    IProjectionRepository projectionRepo,
    ILogger<WorkspaceCopyService> logger) : BaseService(logger), IWorkspaceCopyService
{
    public async Task<Workspace> CopyAsync(Guid templateWorkspaceId, string workspaceName)
    {
        // The caller may be acting on an invite created long ago. Re-check the template here
        // rather than trusting that it still exists and is still a template — otherwise a
        // deleted or unflagged source silently produces an empty workspace.
        var template = await workspaceRepo.GetByIdAsync(templateWorkspaceId);
        if (template is null || !template.IsTemplate)
            throw new TemplateWorkspaceNotFoundException();

        var newWorkspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = workspaceName,
            CreatedAt = DateTime.UtcNow,
            IsTemplate = false,
            SourceTemplateId = templateWorkspaceId,
        };
        await workspaceRepo.CreateAsync(newWorkspace);

        var schemas = await schemaRepo.GetAllByWorkspaceAsync(templateWorkspaceId);
        var tables = await tableRepo.GetAllByWorkspaceAsync(templateWorkspaceId);
        var columns = await columnRepo.GetAllByWorkspaceAsync(templateWorkspaceId);

        // Index the source once. Walking these lists per column instead is quadratic, and a
        // template can hold 100k+ columns.
        var schemasById = schemas.ToDictionary(s => s.Id);
        var tablesById = tables.ToDictionary(t => t.Id);

        // Each level is copied as a single batch, and the ids come back keyed by name — which is
        // what lets the level below resolve its parent without a per-row lookup.
        var newSchemaIds = await schemaRepo.UpsertManyAsync(
            newWorkspace.Id,
            schemas.Select(s => s.Name).ToList());

        var newTableIds = await tableRepo.UpsertManyAsync(
            newWorkspace.Id,
            tables
                .Select(t => (SchemaId: newSchemaIds[schemasById[t.SchemaId].Name], t.Name))
                .ToList());

        await columnRepo.UpsertManyAsync(
            newWorkspace.Id,
            columns
                .Select(c =>
                {
                    var sourceTable = tablesById[c.TableId];
                    var newSchemaId = newSchemaIds[schemasById[sourceTable.SchemaId].Name];
                    return new ColumnImport(newTableIds[(newSchemaId, sourceTable.Name)], c.Name, c.DataType);
                })
                .ToList());

        await projectionRepo.RefreshAsync(newWorkspace.Id);

        Logger.LogInformation(
            "Copied template workspace {TemplateWorkspaceId} to new workspace {NewWorkspaceId}: "
            + "{SchemaCount} schemas, {TableCount} tables, {ColumnCount} columns",
            templateWorkspaceId, newWorkspace.Id, schemas.Count, tables.Count, columns.Count);

        return newWorkspace;
    }
}
