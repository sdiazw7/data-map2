namespace DataMap.Api.Services;

/// <summary>
/// A CSV upload, described independently of the HTTP type it arrived on so the import can be
/// driven — and tested — without an ASP.NET request behind it.
/// </summary>
public record CsvUpload(Stream Content, string FileName, long SizeInBytes);

/// <summary>What an import did, so the caller can report it rather than just a bare 200.</summary>
public record ImportSummary(
    int Rows,
    int Schemas,
    int Tables,
    int ColumnsCreated,
    int ColumnsUpdated);

public interface IMetadataImportService
{
    Task<ImportSummary> ImportCsvAsync(Guid workspaceId, Guid participantId, CsvUpload upload);
}
