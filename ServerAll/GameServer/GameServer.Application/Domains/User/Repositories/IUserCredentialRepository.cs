using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

public interface IUserCredentialRepository
{
    Task<UserCredential> CreateAsync(long userId, string email, string passwordHash, CancellationToken ct = default);
    
    Task<UserCredential?> FindByIdAsync(long userId, CancellationToken ct = default);
    Task<UserCredential?> FindByEmailAsync(string email, CancellationToken ct = default);

    Task<bool> UpdateAsync(UserCredential userCredential, CancellationToken ct = default);
    Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct = default);

    Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 회전으로 물러난 직전 세대 리프레시 토큰의 해시를 기록한다(재사용 탐지용, 휘발성).
    /// </summary>
    Task SetPreviousRefreshTokenAsync(long userId, string hashedToken, DateTime rotatedAt, TimeSpan ttl, CancellationToken ct = default);

    Task<PreviousRefreshToken?> GetPreviousRefreshTokenAsync(long userId, CancellationToken ct = default);

    Task ClearPreviousRefreshTokenAsync(long userId, CancellationToken ct = default);
    
    Task<bool> RemoveAsync(long userId, CancellationToken ct = default);
    
    Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default);
}

/// <summary>
/// 직전 세대 리프레시 토큰 해시와 그것이 물러난 시각. 재사용 탐지와 재시도 유예 판정에 쓴다.
/// </summary>
public sealed record PreviousRefreshToken(string HashedToken, DateTime RotatedAt);
