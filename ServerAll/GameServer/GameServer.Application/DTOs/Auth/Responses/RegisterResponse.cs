using MemoryPack;

namespace GameServer.Application.DTOs.Responses;

[MemoryPackable]
public partial class RegisterResponse
{
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public RegisterResponse(long userId, string userName, string email, DateTime createdAt)
    {
        UserId = userId;
        UserName = userName;
        Email = email;
        CreatedAt = createdAt;
    }
}
