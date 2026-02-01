using MemoryPack;

namespace GameServer.Application.DTOs.Auth.Login;

[MemoryPackable]
public partial class LoginResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string SessionId { get; set; } = "";
    public DateTime ExpiresAt { get; set; }


    public LoginResponse(long userId, string userName,
        string email, string accessToken, 
        string sessionId, DateTime expiresAt)
    {
        UserId = userId;
        UserName = userName;
        Email = email;
        AccessToken = accessToken;
        SessionId = sessionId;
        ExpiresAt = expiresAt;
    }
}