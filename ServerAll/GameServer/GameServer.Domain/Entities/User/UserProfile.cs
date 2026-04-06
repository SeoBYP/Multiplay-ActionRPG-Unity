using System.Text.RegularExpressions;

namespace GameServer.Domain.Entities.User;

/// <summary>
/// 사용자 프로필 정보 Entity
/// </summary>
public class UserProfile
{
    public const int MaxNickNameLength = 20;
    public const int MinNickNameLength = 2;
    /// <summary>
    /// UserID (DB PK)
    /// </summary>
    public long UserId { get; private set; }
    
    /// <summary>
    /// 게임 내에서 표시될 이름 (변경 가능)
    /// </summary>
    public string NickName { get; private set; } = string.Empty;

    public UserProfile() { }

    public UserProfile(long userId, string nickName)
    {
        UserId = userId;
        NickName = nickName;
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
        if (nickname.Length < MinNickNameLength || nickname.Length > MaxNickNameLength)
            throw new ArgumentException($"닉네임은 {MinNickNameLength}~{MaxNickNameLength}자여야 합니다");
        // (한글 + 영문 + 숫자 + 언더스코어)
        var nicknameRegex = new Regex(@"^[\uAC00-\uD7A3a-zA-Z0-9_]+$");
        return nicknameRegex.IsMatch(nickname);
    }


}