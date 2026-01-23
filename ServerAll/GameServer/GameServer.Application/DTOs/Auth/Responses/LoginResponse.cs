using MemoryPack;

namespace GameServer.Application.DTOs.Responses;

[MemoryPackable]
public partial class LoginResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string AccessToken { get; set; } = "";

    public LoginResponse(long userId, string userName, string email, string accessToken)
    {
        UserId = userId;
        UserName = userName;
        Email = email;
        AccessToken = accessToken;
    }
}
