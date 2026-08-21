using DataMap.Api.DTOs;

namespace DataMap.Api.Services;

public interface IInviteService
{
    Task<InviteDto> GetAsync(string token);
    Task<JoinResult> JoinAsync(string token, JoinRequest request);
    Task<CreateInviteResponse> CreateAsync(CreateInviteRequest request, Guid workspaceId);
}
