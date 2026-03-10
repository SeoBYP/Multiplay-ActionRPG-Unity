using GameServer.Application.Common;

namespace GameServer.Application.Domains.Auth.Interfaces;

public interface IAuthService
{
    // Domain Entity 반환
    Task<Result<Domain.Entities.User.User>> RegisterAsync(string password, string email, CancellationToken ct = default);
    
    // 복잡한 경우: 튜플 또는 별도 Result 객체
    Task<Result<LoginResult>> LoginAsync(string email, string password, string deviceId, CancellationToken ct = default);
    
    Task<Result> LogoutAsync(string sessionId, CancellationToken ct = default);
    
    Task<Result<LoginResult>> RefreshTokenAsync(string accessToken, string refreshToken, string deviceId, CancellationToken ct = default);   
    
    Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default);
}