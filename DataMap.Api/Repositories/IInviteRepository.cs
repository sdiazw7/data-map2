using DataMap.Api.Models;

namespace DataMap.Api.Repositories;

public interface IInviteRepository
{
    Task<Invite?> GetByTokenAsync(string token);
    Task IncrementUsedCountAsync(Guid inviteId);
    Task<Invite> CreateAsync(Invite invite);
}
