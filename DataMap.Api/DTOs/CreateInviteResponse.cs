namespace DataMap.Api.DTOs;

public record CreateInviteResponse(
    Guid Id,
    string Token,
    Guid WorkspaceId,
    DateTime ExpiresAt,
    int MaxUses,
    Guid? TemplateWorkspaceId);
