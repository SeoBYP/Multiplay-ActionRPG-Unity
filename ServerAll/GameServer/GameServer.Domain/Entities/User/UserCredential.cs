using System.Text.RegularExpressions;

namespace GameServer.Domain.Entities.User;

/// <summary>
/// 사용자(계정) 인증 식별자 정보 Entity
/// </summary>
public class UserCredential
{
    /// <summary>
    /// UserID (DB PK)
    /// </summary>
    public long UserId { get; private set; }
    
    /// <summary>
    /// 사용자 이메일 주소(사용자 계정)
    /// </summary>
    public string Email { get; private set; } = string.Empty;
    
    /// <summary>
    /// 비밀번호의 해시값
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;
    
    /// <summary>
    /// 사용자 인증을 위한 갱신 토큰
    /// </summary>
    public string? RefreshToken { get; private set; }

    /// <summary>
    /// 리프레시 토큰의 만료 시간을 나타냅니다.
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; private set; }

    private UserCredential()
    {
    }

    public static UserCredential Create(long userId, string email, string passwordHash)
    {
        if (userId <= 0)
            throw new ArgumentException("userId must be greater than zero", nameof(userId));
        if (!IsValidateEmail(email))
            throw new ArgumentException("email is invalid", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("passwordHash cannot be null or whitespace", nameof(passwordHash));

        return new UserCredential
        {
            UserId = userId,
            Email = email,
            PasswordHash = passwordHash
        };
    }

    public static UserCredential Restore(
        long userId,
        string email,
        string passwordHash,
        string? refreshToken,
        DateTime? refreshTokenExpiresAt)
    {
        if (userId <= 0)
            throw new ArgumentException("userId must be greater than zero", nameof(userId));
        if (!IsValidateEmail(email))
            throw new ArgumentException("email is invalid", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("passwordHash cannot be null or whitespace", nameof(passwordHash));

        return new UserCredential
        {
            UserId = userId,
            Email = email,
            PasswordHash = passwordHash,
            RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    /// <summary>
    /// 이메일 주소의 유효성을 검사합니다.
    /// </summary>
    private static bool IsValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return emailRegex.IsMatch(email);
    }
    
    public void SetRefreshToken(string refreshToken, DateTime refreshTokenExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("refreshToken cannot be null or whitespace", nameof(refreshToken));
        if (refreshTokenExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("refreshTokenExpiresAt must be in the future", nameof(refreshTokenExpiresAt));
        
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
    }
    
    public void UpdatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("passwordHash cannot be null or whitespace", nameof(passwordHash));
        PasswordHash = passwordHash;
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = default;
    }
}
