namespace DataMap.Api.Models;

public class Relationship
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid SourceColumnId { get; set; }
    public Guid TargetColumnId { get; set; }
    public string RelationshipType { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
    public Column SourceColumn { get; set; } = null!;
    public Column TargetColumn { get; set; } = null!;
}
