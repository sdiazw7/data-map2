namespace DataMap.Api.Models;

public class Participant
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid InviteId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Invite Invite { get; set; } = null!;
}
