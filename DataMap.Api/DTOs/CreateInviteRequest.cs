namespace DataMap.Api.DTOs;

public record CreateInviteRequest(int MaxUses, DateTime ExpiresAt, Guid? TemplateWorkspaceId);
