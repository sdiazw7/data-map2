namespace DataMap.Api.DTOs;

public record InviteDto(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    DateTime ExpiresAt,
    bool IsValid
);
