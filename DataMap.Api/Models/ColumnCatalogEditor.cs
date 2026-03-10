namespace DataMap.Api.Models;

public class ColumnCatalogEditor
{
    public Guid WorkspaceId { get; set; }
    public Guid ColumnId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? ExampleValue { get; set; }
    public string? Description { get; set; }
    public string? BusinessTerm { get; set; }
    public string? Owner { get; set; }
    public int Version { get; set; }
}
