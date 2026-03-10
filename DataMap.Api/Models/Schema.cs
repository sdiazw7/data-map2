namespace DataMap.Api.Models;

public class Schema
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
}
