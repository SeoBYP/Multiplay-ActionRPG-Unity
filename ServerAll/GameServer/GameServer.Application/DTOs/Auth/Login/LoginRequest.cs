using MemoryPack;

namespace GameServer.Application.DTOs.Auth.Login;

[MemoryPackable]
public partial class LoginRequest
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    
    public LoginRequest(string userName, string password)
    {
        UserName = userName;
        Password = password;
    }
}
