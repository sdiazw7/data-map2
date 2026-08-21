using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using DataMap.Api.Data;
using DataMap.Api.DTOs;
using DataMap.Api.Exceptions;
using DataMap.Api.Repositories;
using Microsoft.Extensions.Logging;

// CsvHelper ships its own ValidationException; alias ours so the body reads unqualified.
using ValidationException = DataMap.Api.Exceptions.ValidationException;

namespace DataMap.Api.Services;

/// <summary>
/// Owns CSV ingestion: validating the upload, parsing it, and writing the catalog it describes.
/// Kept apart from <see cref="MetadataService"/> because the two share nothing but a workspace id —
/// one is a bulk structural import, the other serves and edits the grid.
/// </summary>
public class MetadataImportService(
    IColumnRepository columnRepo,
    ISchemaRepository schemaRepo,
    ITableRepository tableRepo,
    IProjectionService projectionService,
    IUnitOfWork unitOfWork,
    ILogger<MetadataImportService> logger) : BaseService(logger), IMetadataImportService
{
    private const long MaxUploadBytes = 25L * 1024 * 1024;
    private const int MaxCsvRows = 200_000;
    private const int MaxReportedRowErrors = 10;
    private const int MaxIdentifierLength = 200;
    private const string ExpectedHeaders = "schema_name, table_name, column_name, data_type";

    public async Task<ImportSummary> ImportCsvAsync(Guid workspaceId, Guid participantId, CsvUpload upload)
    {
        var records = ReadCsv(upload);

        return await unitOfWork.ExecuteAsync(async () =>
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

            var summary = new ImportSummary(
                records.Count,
                schemaNames.Distinct(StringComparer.Ordinal).Count(),
                tableKeys.Count,
                result.Created,
                result.Updated);

            Logger.LogInformation(
                "CSV upload complete for workspace {WorkspaceId} by participant {ParticipantId}: "
                + "{RowCount} rows, {SchemaCount} schemas, {TableCount} tables, "
                + "{CreatedCount} columns created, {UpdatedCount} columns updated",
                workspaceId, participantId, summary.Rows, summary.Schemas, summary.Tables,
                summary.ColumnsCreated, summary.ColumnsUpdated);

            return summary;
        });
    }

    /// <summary>Parses the upload and validates it, so a bad file fails as a 400 rather than a 500.</summary>
    private static List<CsvColumnRecord> ReadCsv(CsvUpload upload)
    {
        Require(upload.SizeInBytes > 0, "The uploaded file is empty.");
        Require(upload.SizeInBytes <= MaxUploadBytes,
            $"The file is {upload.SizeInBytes / (1024 * 1024)} MB, over the {MaxUploadBytes / (1024 * 1024)} MB limit.");
        Require(upload.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase),
            "Only .csv files are accepted.");

        List<CsvColumnRecord> parsed;
        using (var reader = new StreamReader(upload.Content))
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
                throw new ValidationException(
                    $"The CSV could not be read. Expected headers: {ExpectedHeaders}.");
            }
        }

        Require(parsed.Count > 0, $"The CSV has no data rows. Expected headers: {ExpectedHeaders}.");
        Require(parsed.Count <= MaxCsvRows,
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
            throw new ValidationException($"The CSV has invalid rows: {string.Join("; ", errors)}.");

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
