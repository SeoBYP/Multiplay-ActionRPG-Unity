using GameServer.Application.Common;
using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;
using User = Domain.Entities.User.User;
public interface IUserProfileService
{
    Task<Result<UserProfile>> GetProfileAsync(string sessionId, CancellationToken ct = default);    
    
    Task<Result<UserProfile>> UpdateProfileAsync(string sessionId, string nickname, CancellationToken ct = default);
}