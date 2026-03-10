namespace DataMap.Api.Models;

public class ParticipantSession
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid WorkspaceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public Participant Participant { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
}
