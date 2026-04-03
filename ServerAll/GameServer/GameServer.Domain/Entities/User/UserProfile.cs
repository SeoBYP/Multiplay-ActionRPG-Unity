using System.Text.RegularExpressions;

namespace GameServer.Domain.Entities.User;

/// <summary>
/// 사용자 프로필 정보 Entity
/// </summary>
public class UserProfile
{
    /// <summary>
    /// UserID (DB PK)
    /// </summary>
    public long UserId { get; private set; }
    
    /// <summary>
    /// 게임 내에서 표시될 이름 (변경 가능)
    /// </summary>
    public string NickName { get; private set; } = string.Empty;

    public UserProfile(long userId, string nickname)
    {
        UserId = userId;
        NickName = nickname;
    }
    
    public static UserProfile Create(long userId, string nickname)
    {
        if(!IsValidateNickname(nickname))
            throw new ArgumentException("Nickname is invalid", nameof(nickname));
        
        return new UserProfile(userId, nickname);
    }
    
    public static UserProfile FromRedis(long userId, string nickname)
    {
        return new UserProfile(userId, nickname);
    }
    
    public void SetNickName(string nickname)
    {
        if(!IsValidateNickname(nickname))
            throw new ArgumentException("Nickname is invalid", nameof(nickname));
        NickName = nickname;
    }
    
    /// <summary>
    /// 닉네임의 유효성을 검사합니다. (한글 + 영문 + 숫자 + 언더스코어)
    /// </summary>
    private static bool IsValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("닉네임은 비어있을 수 없습니다");
        if (nickname.Length < 2 || nickname.Length > 20)
            throw new ArgumentException("닉네임은 2~20자여야 합니다");
        // (한글 + 영문 + 숫자 + 언더스코어)
        var nicknameRegex = new Regex(@"^[\uAC00-\uD7A3a-zA-Z0-9_]+$");
        return nicknameRegex.IsMatch(nickname);
    }


}