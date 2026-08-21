namespace DataMap.Api.Models;

public class Column
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid TableId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? ExampleValue { get; set; }
    public string? Description { get; set; }
    public string? Owner { get; set; }
    public Guid? BusinessTermId { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Table Table { get; set; } = null!;
    public BusinessTerm? BusinessTerm { get; set; }
}
