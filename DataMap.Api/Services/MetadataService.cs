using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using DataMap.Api.Data;
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
    IUnitOfWork unitOfWork,
    ILogger<MetadataService> logger) : BaseService(logger), IMetadataService
{
    private static readonly HashSet<string> SortableColumns = ["column_name", "table_name", "data_type", "owner"];

    // Paging bounds. A workspace holds 100k+ columns, so an unbounded limit would read the
    // whole catalog into memory and defeat the point of paginating at all.
    private const int MaxLimit = 1000;

    // CSV upload bounds.
    private const long MaxUploadBytes = 25L * 1024 * 1024;
    private const int MaxCsvRows = 200_000;
    private const int MaxReportedRowErrors = 10;
    private const int MaxIdentifierLength = 200;
    private const string ExpectedHeaders = "schema_name, table_name, column_name, data_type";

    // Bulk edit bounds. The grid pastes in chunks; this caps one request, not one session.
    private const int MaxBulkUpdateRows = 5_000;
    private const int MaxDescriptionLength = 4_000;
    private const int MaxExampleValueLength = 1_000;
    private const int MaxOwnerLength = 200;

    public async Task<List<ColumnGridRow>> GetColumnsAsync(Guid workspaceId, MetadataColumnsQuery query)
    {
        if (!SortableColumns.Contains(query.SortBy))
            throw new Exceptions.ValidationException($"'{query.SortBy}' is not a sortable field. Allowed values: {string.Join(", ", SortableColumns)}.");

        if (query.SortDir != "asc" && query.SortDir != "desc")
            throw new Exceptions.ValidationException("sort_dir must be 'asc' or 'desc'.");

        if (query.Limit < 1 || query.Limit > MaxLimit)
            throw new Exceptions.ValidationException($"limit must be between 1 and {MaxLimit}.");

        if (query.Offset < 0)
            throw new Exceptions.ValidationException("offset must be zero or greater.");

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
        var records = ReadCsv(file);

        await unitOfWork.ExecuteAsync(async () =>
        {
            // Each level is upserted as one batch, so the import costs a fixed handful of round
            // trips rather than a pair per row. Ids come back as lookups for the level below.
            var schemaNames = records.Select(r => r.SchemaName).ToList();
            var schemaIds = await schemaRepo.UpsertManyAsync(workspaceId, schemaNames);

            var tableKeys = records
                .Select(r => (SchemaId: schemaIds[r.SchemaName], Name: r.TableName))
                .Distinct()
                .ToList();
            var tableIds = await tableRepo.UpsertManyAsync(workspaceId, tableKeys);

            var imports = records
                .Select(r => new ColumnImport(
                    tableIds[(schemaIds[r.SchemaName], r.TableName)],
                    r.ColumnName,
                    r.DataType))
                .ToList();

            var result = await columnRepo.UpsertManyAsync(workspaceId, imports);
            if (result.Conflicted)
                throw new VersionConflictException();

            // Structural change, so the projection is rebuilt wholesale rather than synced.
            // It runs inside this transaction: the rebuild deletes before it reinserts, and
            // committing that separately would expose an empty catalog to readers.
            await projectionService.RefreshAsync(workspaceId);

            Logger.LogInformation(
                "CSV upload complete for workspace {WorkspaceId} by participant {ParticipantId}: "
                + "{RowCount} rows, {SchemaCount} schemas, {TableCount} tables, "
                + "{CreatedCount} columns created, {UpdatedCount} columns updated",
                workspaceId, participantId, records.Count,
                schemaNames.Distinct(StringComparer.Ordinal).Count(), tableKeys.Count,
                result.Created, result.Updated);
        });
    }

    public async Task BulkUpdateAsync(Guid workspaceId, Guid participantId, List<ColumnUpdateRequest> updates)
    {
        ValidateUpdates(updates);

        // Read and check every target before writing any of them. The repositories each commit
        // on their own, so a version conflict discovered mid-write used to leave the earlier
        // columns saved while their audit records and projection sync were dropped on the throw.
        var columns = await columnRepo.GetByIdsAsync(
            workspaceId,
            updates.Select(u => u.ColumnId).ToList());
        var columnsById = columns.ToDictionary(c => c.Id);

        foreach (var update in updates)
        {
            if (!columnsById.TryGetValue(update.ColumnId, out var column))
            {
                Logger.LogWarning(
                    "Column {ColumnId} not found in workspace {WorkspaceId} — skipping",
                    update.ColumnId, workspaceId);
                continue;
            }

            if (column.Version != update.Version)
                throw new VersionConflictException();
        }

        var now = DateTime.UtcNow;
        var changes = new List<MetadataChange>();
        var edited = new List<Column>();

        foreach (var update in updates)
        {
            if (!columnsById.TryGetValue(update.ColumnId, out var column)) continue;

            changes.AddRange(ApplyUpdate(column, update, participantId, now));
            edited.Add(column);
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            // The Version check above is a read-then-write, so it cannot see a writer that
            // commits in between. Version is also an EF concurrency token, which puts the
            // value that was read into the UPDATE's WHERE clause and makes the database
            // reject the losing write instead of silently overwriting the winner.
            if (!await columnRepo.UpdateRangeAsync(edited))
                throw new VersionConflictException();

            if (changes.Count > 0)
                await changeRepo.AddRangeAsync(changes);

            // Sync only the rows that changed. Rebuilding the whole projection here would cost
            // a full delete + reinsert of the workspace on every keystroke-level edit.
            await projectionService.SyncColumnsAsync(workspaceId, edited);
        });
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

    /// <summary>Copies the request onto the column, returning an audit record per changed field.</summary>
    private static List<MetadataChange> ApplyUpdate(
        Column column, ColumnUpdateRequest update, Guid participantId, DateTime now)
    {
        var changes = new List<MetadataChange>();

        void Track(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;

            changes.Add(new MetadataChange
            {
                Id = Guid.NewGuid(),
                EntityType = "Column",
                EntityId = column.Id,
                Field = field,
                OldValue = oldValue,
                NewValue = newValue,
                ParticipantId = participantId,
                EditedAt = now
            });
        }

        Track("Description", column.Description, update.Description);
        Track("ExampleValue", column.ExampleValue, update.ExampleValue);
        Track("Owner", column.Owner, update.Owner);

        column.Description = update.Description;
        column.ExampleValue = update.ExampleValue;
        column.Owner = update.Owner;
        column.Version++;
        column.UpdatedAt = now;

        return changes;
    }

    private static void ValidateUpdates(List<ColumnUpdateRequest> updates)
    {
        if (updates.Count == 0)
            throw new Exceptions.ValidationException("No column updates were supplied.");

        if (updates.Count > MaxBulkUpdateRows)
            throw new Exceptions.ValidationException(
                $"A single request may update at most {MaxBulkUpdateRows:N0} columns; received {updates.Count:N0}.");

        // Two edits to one column in one batch would bump Version twice and write an audit
        // trail that reads as though the first edit never happened.
        var duplicate = updates
            .GroupBy(u => u.ColumnId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new Exceptions.ValidationException($"Column {duplicate.Key} appears more than once in the request.");

        foreach (var update in updates)
        {
            RequireMaxLength(update.Description, MaxDescriptionLength, "description", update.ColumnId);
            RequireMaxLength(update.ExampleValue, MaxExampleValueLength, "example value", update.ColumnId);
            RequireMaxLength(update.Owner, MaxOwnerLength, "owner", update.ColumnId);
        }
    }

    private static void RequireMaxLength(string? value, int maxLength, string field, Guid columnId)
    {
        if (value is not null && value.Length > maxLength)
            throw new Exceptions.ValidationException(
                $"The {field} for column {columnId} is longer than the {maxLength:N0} character limit.");
    }

    /// <summary>Parses the upload and validates it, so a bad file fails as a 400 rather than a 500.</summary>
    private static List<CsvColumnRecord> ReadCsv(IFormFile file)
    {
        if (file.Length == 0)
            throw new Exceptions.ValidationException("The uploaded file is empty.");

        if (file.Length > MaxUploadBytes)
            throw new Exceptions.ValidationException(
                $"The file is {file.Length / (1024 * 1024)} MB, over the {MaxUploadBytes / (1024 * 1024)} MB limit.");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new Exceptions.ValidationException("Only .csv files are accepted.");

        List<CsvColumnRecord> parsed;
        using (var reader = new StreamReader(file.OpenReadStream()))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            try
            {
                parsed = csv.GetRecords<CsvColumnRecord>().ToList();
            }
            catch (CsvHelperException)
            {
                // A missing header or a ragged row is the uploader's mistake. Left uncaught it
                // reaches the error middleware as an unknown fault and reports a 500.
                throw new Exceptions.ValidationException(
                    $"The CSV could not be read. Expected headers: {ExpectedHeaders}.");
            }
        }

        if (parsed.Count == 0)
            throw new Exceptions.ValidationException($"The CSV has no data rows. Expected headers: {ExpectedHeaders}.");

        if (parsed.Count > MaxCsvRows)
            throw new Exceptions.ValidationException(
                $"The CSV has {parsed.Count:N0} rows, over the {MaxCsvRows:N0} row limit. Please split it into smaller files.");

        var errors = new List<string>();
        var records = new List<CsvColumnRecord>(parsed.Count);

        for (var i = 0; i < parsed.Count && errors.Count < MaxReportedRowErrors; i++)
        {
            var lineNumber = i + 2; // row 1 is the header
            var record = new CsvColumnRecord
            {
                SchemaName = parsed[i].SchemaName?.Trim() ?? string.Empty,
                TableName = parsed[i].TableName?.Trim() ?? string.Empty,
                ColumnName = parsed[i].ColumnName?.Trim() ?? string.Empty,
                DataType = parsed[i].DataType?.Trim() ?? string.Empty,
            };

            var before = errors.Count;
            RequireIdentifier(record.SchemaName, "schema_name", lineNumber, errors);
            RequireIdentifier(record.TableName, "table_name", lineNumber, errors);
            RequireIdentifier(record.ColumnName, "column_name", lineNumber, errors);
            RequireIdentifier(record.DataType, "data_type", lineNumber, errors);

            if (errors.Count == before)
                records.Add(record);
        }

        if (errors.Count > 0)
            throw new Exceptions.ValidationException($"The CSV has invalid rows: {string.Join("; ", errors)}.");

        return records;
    }

    private static void RequireIdentifier(string value, string field, int lineNumber, List<string> errors)
    {
        if (string.IsNullOrEmpty(value))
            errors.Add($"row {lineNumber}: {field} is required");
        else if (value.Length > MaxIdentifierLength)
            errors.Add($"row {lineNumber}: {field} is longer than {MaxIdentifierLength} characters");
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
