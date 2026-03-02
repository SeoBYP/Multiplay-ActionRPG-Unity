using GameServer.Application.Common;
using GameServer.Domain.Entities.User;

namespace GameServer.Application.Services.Auth.Interfaces;

using GameServer.Domain.Entities.User;

public interface IAuthService
{
    // Domain Entity 반환
    Task<Result<User>> RegisterAsync(string password, string email, CancellationToken ct = default);
    
    // 복잡한 경우: 튜플 또는 별도 Result 객체
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken ct = default);
    
    Task<Result> LogoutAsync(string sessionId, CancellationToken ct = default);
    
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}