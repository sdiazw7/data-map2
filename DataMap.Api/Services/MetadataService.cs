using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Models;
using DataMap.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DataMap.Api.Services;

public class MetadataService(
    IColumnRepository columnRepo,
    ISchemaRepository schemaRepo,
    ITableRepository tableRepo,
    IProjectionRepository projectionRepo,
    IMetadataChangeRepository changeRepo,
    IProjectionService projectionService,
    ILogger<MetadataService> logger) : BaseService(logger), IMetadataService
{
    private static readonly HashSet<string> SortableColumns = ["column_name", "table_name", "data_type", "owner"];

    public async Task<List<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query)
    {
        if (!SortableColumns.Contains(query.SortBy))
            throw new Exceptions.ValidationException($"'{query.SortBy}' is not a sortable field. Allowed values: {string.Join(", ", SortableColumns)}.");

        if (query.SortDir != "asc" && query.SortDir != "desc")
            throw new Exceptions.ValidationException("sort_dir must be 'asc' or 'desc'.");

        var rows = await projectionRepo.QueryAsync(
            workspaceId,
            query.Limit,
            query.Offset,
            query.Search,
            query.UndocumentedOnly,
            query.TableName,
            query.SortBy,
            query.SortDir);

        return rows.Select(r => new ColumnGridRow(
            r.ColumnId,
            r.SchemaName,
            r.TableName,
            r.ColumnName,
            r.DataType,
            r.ExampleValue,
            r.Description,
            r.BusinessTerm,
            r.Owner,
            r.Version
        )).ToList();
    }

    public async Task UploadCsvAsync(Guid workspaceId, Guid participantId, IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<CsvColumnRecord>().ToList();

        var schemaCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var tableCache = new Dictionary<(string Schema, string Table), Guid>();

        int schemaCount = 0;
        int tableCount = 0;
        int columnCount = 0;

        foreach (var record in records)
        {
            if (!schemaCache.TryGetValue(record.SchemaName, out var schemaId))
            {
                var schema = await schemaRepo.UpsertAsync(workspaceId, record.SchemaName);
                schemaId = schema.Id;
                schemaCache[record.SchemaName] = schemaId;
                schemaCount++;
            }

            var tableKey = (record.SchemaName, record.TableName);
            if (!tableCache.TryGetValue(tableKey, out var tableId))
            {
                var table = await tableRepo.UpsertAsync(workspaceId, schemaId, record.TableName);
                tableId = table.Id;
                tableCache[tableKey] = tableId;
                tableCount++;
            }

            await columnRepo.UpsertAsync(workspaceId, tableId, record.ColumnName, record.DataType);
            columnCount++;
        }

        Logger.LogInformation(
            "CSV upload complete for workspace {WorkspaceId} by participant {ParticipantId}: {SchemaCount} schemas, {TableCount} tables, {ColumnCount} columns",
            workspaceId, participantId, schemaCount, tableCount, columnCount);

        await projectionService.RefreshAsync(workspaceId);
    }

    public async Task BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates)
    {
        var changes = new List<MetadataChange>();
        var now = DateTime.UtcNow;

        foreach (var update in updates)
        {
            var column = await columnRepo.GetByIdAsync(workspaceId, update.ColumnId);
            if (column is null)
            {
                Logger.LogWarning(
                    "Column {ColumnId} not found in workspace {WorkspaceId} — skipping",
                    update.ColumnId, workspaceId);
                continue;
            }

            if (column.Version != update.Version)
                throw new VersionConflictException();

            if (column.Description != update.Description)
            {
                changes.Add(new MetadataChange
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Column",
                    EntityId = column.Id,
                    Field = "Description",
                    OldValue = column.Description,
                    NewValue = update.Description,
                    ParticipantId = participantId,
                    EditedAt = now
                });
                column.Description = update.Description;
            }

            if (column.ExampleValue != update.ExampleValue)
            {
                changes.Add(new MetadataChange
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Column",
                    EntityId = column.Id,
                    Field = "ExampleValue",
                    OldValue = column.ExampleValue,
                    NewValue = update.ExampleValue,
                    ParticipantId = participantId,
                    EditedAt = now
                });
                column.ExampleValue = update.ExampleValue;
            }

            if (column.Owner != update.Owner)
            {
                changes.Add(new MetadataChange
                {
                    Id = Guid.NewGuid(),
                    EntityType = "Column",
                    EntityId = column.Id,
                    Field = "Owner",
                    OldValue = column.Owner,
                    NewValue = update.Owner,
                    ParticipantId = participantId,
                    EditedAt = now
                });
                column.Owner = update.Owner;
            }

            column.Version++;
            column.UpdatedAt = now;

            await columnRepo.UpdateAsync(column);
        }

        if (changes.Count > 0)
            await changeRepo.AddRangeAsync(changes);

        await projectionService.RefreshAsync(workspaceId);
    }

    public async Task<CoverageResponse> GetCoverageAsync(Guid workspaceId)
    {
        var (total, documented) = await projectionRepo.GetCoverageCountsAsync(workspaceId);
        var percent = total == 0 ? 0.0 : Math.Round((double)documented / total * 100, 1);
        return new CoverageResponse(total, documented, percent);
    }

    public async Task<List<string>> GetTableNamesAsync(Guid workspaceId)
    {
        return await projectionRepo.GetDistinctTableNamesAsync(workspaceId);
    }

    private sealed record CsvColumnRecord
    {
        [Name("schema_name")]
        public string SchemaName { get; init; } = string.Empty;

        [Name("table_name")]
        public string TableName { get; init; } = string.Empty;

        [Name("column_name")]
        public string ColumnName { get; init; } = string.Empty;

        [Name("data_type")]
        public string DataType { get; init; } = string.Empty;
    }
}
