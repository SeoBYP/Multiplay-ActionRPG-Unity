using MemoryPack;

namespace GameServer.Application.DTOs.Auth.Register;

[MemoryPackable]
public partial class RegisterRequest
{
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string Email { get; set; } = "";
    
    public RegisterRequest(string userName, string password, string email)
    {
        UserName = userName;
        Password = password;
        Email = email;
    }
}
