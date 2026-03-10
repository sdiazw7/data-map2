namespace DataMap.Api.Models;

public class Invite
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }

    public Workspace Workspace { get; set; } = null!;
}
