namespace DataMap.Api.Models;

public class Table
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid SchemaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Schema Schema { get; set; } = null!;
}
