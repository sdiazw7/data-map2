namespace DataMap.Api.DTOs;

/// <summary>What an import did, so the caller can report it rather than just a bare 200.</summary>
public record ImportSummary(
    int Rows,
    int Schemas,
    int Tables,
    int ColumnsCreated,
    int ColumnsUpdated);
