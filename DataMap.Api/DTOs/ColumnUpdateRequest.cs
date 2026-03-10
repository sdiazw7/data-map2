namespace DataMap.Api.DTOs;

public record ColumnUpdateRequest(
    Guid ColumnId,
    string? Description,
    string? ExampleValue,
    string? Owner,
    int Version
);
