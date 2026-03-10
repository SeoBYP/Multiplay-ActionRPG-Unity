using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.Auth;
using User = Domain.Entities.User.User;
public class LoginResult(User user, UserSession session, string accessToken, string refreshToken, DateTime expiresAt)
{
    public User User { get; } = user;
    public UserSession Session { get; } = session;
    public string AccessToken { get; } = accessToken;
    public string RefreshToken { get; } = refreshToken;
    public DateTime ExpiresAt { get; } = expiresAt; // ← 추가
}
