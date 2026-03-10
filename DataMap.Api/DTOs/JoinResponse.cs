namespace DataMap.Api.DTOs;

public record JoinResponse(
    Guid ParticipantId,
    Guid WorkspaceId,
    string WorkspaceName,
    string Email
);
