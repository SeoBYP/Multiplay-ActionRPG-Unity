using System.Text.RegularExpressions;
using NanoidDotNet;

namespace GameServer.Domain.Entities.User;

/// <summary>
/// 사용자(계정) 정보를 담는 도메인 엔티티 클래스
/// </summary>
public class User
{
    public const int MaxPublicIdLength = 10;
    
    /// <summary>
    /// 내부 식별자 (DB PK)
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    /// 친구 추가나 검색 등에 사용되는 공개용 ID
    /// </summary>
    public string PublicId { get; private set; } = string.Empty;
    
    /// <summary>
    /// 계정 생성 일시
    /// </summary>
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

    /// <summary>
    /// 새로운 User 객체를 생성합니다.
    /// </summary>
    /// <returns>생성된 User 인스턴스</returns>
    /// <exception cref="ArgumentException">입력값이 유효하지 않을 경우 발생</exception>
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