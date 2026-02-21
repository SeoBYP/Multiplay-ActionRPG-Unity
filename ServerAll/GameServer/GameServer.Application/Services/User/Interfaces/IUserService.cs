using GameServer.Application.Common;

namespace GameServer.Application.Services.User.Interfaces;
using User = Domain.Entities.User.User;
public interface IUserService
{
    Task<Result<User>> GetProfileAsync(string sessionId);    
    
    Task<Result<User>> SetNicknameAsync(string sessionId, string nickname);
    
    Task<Result<User>> SetEmailAsync(string sessionId, string email);
    Task<Result<User>> UpdateProfileAsync(string sessionId, string nickname, string email);
}