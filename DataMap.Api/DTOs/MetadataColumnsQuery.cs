namespace DataMap.Api.DTOs;

public record MetadataColumnsQuery(
    int Limit = 200,
    int Offset = 0,
    string? Search = null,
    bool UndocumentedOnly = false,
    string? TableName = null
);
