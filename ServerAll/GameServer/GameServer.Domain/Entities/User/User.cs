using System.Text.RegularExpressions;
using NanoidDotNet;

namespace GameServer.Domain.Entities.User;

public class User
{
    public const int MaxPublicIdLength = 10;
    
    public long UserId { get; private set; }

    /// <summary>친구 추가·검색 등에 쓰는 공개용 ID</summary>
    public string PublicId { get; private set; } = string.Empty;
    
    public DateTime CreatedAt { get; private set; }
    
    public static User FromRedis(long userId, string publicId, DateTime createdAt)
    {
        return new User
        {
            UserId = userId,
            PublicId = publicId,
            CreatedAt = createdAt,
        };
    }

    private User(){ }

    public static User Create()
    {
        var publicId = Nanoid.Generate(Const.AllowedPublicIdChars, size:MaxPublicIdLength);
        
        if(publicId == null)
            throw new InvalidOperationException("Failed to generate public id");
        
        return new User
        {
            PublicId = publicId,
            CreatedAt = DateTime.UtcNow
        };
    }
}