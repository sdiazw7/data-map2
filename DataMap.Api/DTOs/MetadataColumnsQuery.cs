namespace DataMap.Api.DTOs;

/// <remarks>
/// <c>SortBy</c> takes the response field names (<c>columnName</c>, <c>tableName</c>,
/// <c>dataType</c>, <c>owner</c>) so a caller sorts by the same identifier it reads back,
/// rather than translating into a second vocabulary of storage-side names.
/// </remarks>
public record MetadataColumnsQuery(
    int Limit = 200,
    int Offset = 0,
    string? Search = null,
    bool UndocumentedOnly = false,
    string? TableName = null,
    string SortBy = "columnName",
    string SortDir = "asc"
);
