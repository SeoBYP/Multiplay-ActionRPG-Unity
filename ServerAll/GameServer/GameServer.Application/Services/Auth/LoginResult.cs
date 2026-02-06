using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;

namespace GameServer.Application.Services.Auth;

public class LoginResult(User user, UserSession session, string accessToken, DateTime expiresAt)
{
    public User User { get; } = user;
    public UserSession Session { get; } = session;
    public string AccessToken { get; } = accessToken;
    public DateTime ExpiresAt { get; } = expiresAt; // ← 추가
}
