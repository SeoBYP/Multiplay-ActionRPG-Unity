using System.Text.RegularExpressions;

namespace GameServer.Domain.Entities.User;

public class UserCredential
{
    public long UserId { get; private set; }
    
    public string Email { get; private set; } = string.Empty;
    
    public string PasswordHash { get; private set; } = string.Empty;
    
    public string? RefreshToken { get; private set; }

    public int RefreshTokenVersion { get; private set; } = 0;
    
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
        int refreshTokenVersion,
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
            RefreshTokenVersion = refreshTokenVersion,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

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
        RefreshTokenVersion++;
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
