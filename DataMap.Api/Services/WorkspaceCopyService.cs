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
        var now = DateTime.UtcNow;

        var newWorkspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = workspaceName,
            CreatedAt = now,
            IsTemplate = false,
            SourceTemplateId = templateWorkspaceId,
        };
        await workspaceRepo.CreateAsync(newWorkspace);

        var schemas = await schemaRepo.GetAllByWorkspaceAsync(templateWorkspaceId);
        var tables = await tableRepo.GetAllByWorkspaceAsync(templateWorkspaceId);
        var columns = await columnRepo.GetAllByWorkspaceAsync(templateWorkspaceId);

        // Map old schema IDs → new schema IDs
        var schemaIdMap = new Dictionary<Guid, Guid>();
        foreach (var schema in schemas)
        {
            var newId = Guid.NewGuid();
            schemaIdMap[schema.Id] = newId;
            await schemaRepo.UpsertAsync(newWorkspace.Id, schema.Name);
            // UpsertAsync creates with a generated ID, so we re-fetch to get the actual ID for the table map
        }

        // Re-fetch new schemas to build accurate ID map for tables
        var newSchemas = await schemaRepo.GetAllByWorkspaceAsync(newWorkspace.Id);
        var newSchemaByName = newSchemas.ToDictionary(s => s.Name, s => s.Id);

        // Map old table IDs → new table IDs
        var tableIdMap = new Dictionary<Guid, Guid>();
        foreach (var table in tables)
        {
            var templateSchema = schemas.First(s => s.Id == table.SchemaId);
            var newSchemaId = newSchemaByName[templateSchema.Name];
            await tableRepo.UpsertAsync(newWorkspace.Id, newSchemaId, table.Name);
        }

        // Re-fetch new tables to build accurate ID map for columns
        var newTables = await tableRepo.GetAllByWorkspaceAsync(newWorkspace.Id);
        var newTableBySchemaAndName = newTables.ToDictionary(
            t => (SchemaId: t.SchemaId, t.Name),
            t => t.Id);

        foreach (var column in columns)
        {
            var templateTable = tables.First(t => t.Id == column.TableId);
            var templateSchema = schemas.First(s => s.Id == templateTable.SchemaId);
            var newSchemaId = newSchemaByName[templateSchema.Name];
            var newTableId = newTableBySchemaAndName[(newSchemaId, templateTable.Name)];
            await columnRepo.UpsertAsync(newWorkspace.Id, newTableId, column.Name, column.DataType);
        }

        await projectionRepo.RefreshAsync(newWorkspace.Id);

        Logger.LogInformation("Copied template workspace {TemplateWorkspaceId} to new workspace {NewWorkspaceId}",
            templateWorkspaceId, newWorkspace.Id);

        return newWorkspace;
    }
}
