using System.Text.RegularExpressions;

namespace GameServer.Domain.Entities;

public class User
{
    public long UserId { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private User(){ }

    public static User Create(string userName, string password, string email)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username cannot be null or whitespace", nameof(userName));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
        
        if(userName.Length < 3 || userName.Length > 20)
            throw new ArgumentException("Username length must be between 3 and 20 characters", nameof(userName));

        if (!IsValidateUsername(userName))
            throw new ArgumentException("Username can only contain letters, numbers, and underscores", nameof(userName));
        
        if(!IsValidateEmail(email))
            throw new ArgumentException("Email is invalid", nameof(email));
        
        return new User
        {
            UserName = userName,
            PasswordHash = password,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetUserId(long userId)
    {
        if (UserId != 0)
            throw new InvalidOperationException("UserId already set");
        UserId = userId;
    }

    private static bool IsValidateUsername(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return false;
        // 영문, 숫자, 언더스코어만 허용
        var usernameRegex = new Regex(@"^[a-zA-Z0-9_]+$");
        return usernameRegex.IsMatch(userName);
    }

    private static bool IsValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return emailRegex.IsMatch(email);
    }
}