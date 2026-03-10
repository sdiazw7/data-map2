namespace DataMap.Api.DTOs;

public record ColumnGridRow(
    Guid ColumnId,
    string SchemaName,
    string TableName,
    string ColumnName,
    string DataType,
    string? ExampleValue,
    string? Description,
    string? BusinessTerm,
    string? Owner,
    int Version
);
