using GameServer.Application.Common;

namespace GameServer.Application.Domains.User.Interfaces;
using User = Domain.Entities.User.User;
public interface IUserService
{
    Task<Result<User>> GetProfileAsync(string sessionId, CancellationToken ct = default);    
    
    Task<Result<User>> SetNicknameAsync(string sessionId, string nickname, CancellationToken ct = default);
    
    Task<Result<User>> SetEmailAsync(string sessionId, string email, CancellationToken ct = default);
    Task<Result<User>> UpdateProfileAsync(string sessionId, string nickname, string email, CancellationToken ct = default);
}