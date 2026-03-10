namespace DataMap.Api.Models;

public class MetadataChange
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public Guid ParticipantId { get; set; }
    public DateTime EditedAt { get; set; }

    public Participant Participant { get; set; } = null!;
}
