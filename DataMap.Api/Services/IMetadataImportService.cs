using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

/// <summary>
/// A CSV upload, described independently of the HTTP type it arrived on so the import can be
/// driven — and tested — without an ASP.NET request behind it.
/// </summary>
public record CsvUpload(Stream Content, string FileName, long SizeInBytes);

public interface IMetadataImportService
{
    Task<ImportSummary> ImportCsvAsync(Guid workspaceId, Guid participantId, CsvUpload upload);
}
