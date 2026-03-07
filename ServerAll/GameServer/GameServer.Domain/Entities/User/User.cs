using System.Text.RegularExpressions;
using NanoidDotNet;

namespace GameServer.Domain.Entities.User;

/// <summary>
/// 사용자(계정) 정보를 담는 도메인 엔티티 클래스
/// </summary>
public class User
{
    /// <summary>
    /// 내부 식별자 (DB PK)
    /// </summary>
    public long UserId { get; private set; }

    /// <summary>
    /// 친구 추가나 검색 등에 사용되는 공개용 ID
    /// </summary>
    public string PublicId { get; private set; } = string.Empty;

    /// <summary>
    /// 게임 내에서 표시될 이름 (변경 가능)
    /// </summary>
    public string NickName { get; private set; } = string.Empty;

    /// <summary>
    /// 비밀번호의 해시값
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// 사용자 이메일 주소
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// 계정 생성 일시
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 사용자 인증을 위한 갱신 토큰
    /// </summary>
    public string? RefreshToken { get; private set; }

    /// <summary>
    /// 리프레시 토큰의 만료 시간을 나타냅니다.
    /// </summary>
    public DateTime RefreshTokenExpiresAt { get; private set; }

    public static User FromRedis(long userId, string email, string publicId, string passwordHash, DateTime createdAt, string nickName, string? refreshToken = null, DateTime refreshTokenExpiresAt = default)
    {
        return new User
        {
            UserId = userId,
            NickName = nickName,
            Email = email,
            PublicId = publicId,
            PasswordHash = passwordHash,
            CreatedAt = createdAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    private User(){ }

    /// <summary>
    /// 새로운 User 객체를 생성합니다.
    /// </summary>
    /// <param name="password">비밀번호 (해시 권장)</param>
    /// <param name="email">이메일 주소</param>
    /// <returns>생성된 User 인스턴스</returns>
    /// <exception cref="ArgumentException">입력값이 유효하지 않을 경우 발생</exception>
    public static User Create(string password, string email)
    {

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
        
        if(!IsValidateEmail(email))
            throw new ArgumentException("Email is invalid", nameof(email));
        
        var publicId = Nanoid.Generate(Const.AllowedPublicIdChars, size:10);
        
        if(publicId == null)
            throw new InvalidOperationException("Failed to generate public id");
        
        return new User
        {
            PublicId = publicId,
            PasswordHash = password,
            NickName = $"Guest_{publicId}",
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 사용자 ID를 설정합니다. (생성 시 1회만 가능)
    /// </summary>
    /// <param name="userId">설정할 사용자 ID</param>
    /// <exception cref="InvalidOperationException">ID가 이미 설정되어 있을 때 발생</exception>
    public void SetUserId(long userId)
    {
        if (UserId != 0)
            throw new InvalidOperationException("UserId already set");
        UserId = userId;
    }

    public void SetNickName(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            throw new ArgumentException("nickname cannot be null or whitespace", nameof(nickname));
        
        if(nickname.Length < 3 || nickname.Length > 20)
            throw new ArgumentException("nickname length must be between 3 and 20 characters", nameof(nickname));
        
        if(!IsValidateNickname(nickname))
            throw new ArgumentException("Nickname is invalid", nameof(nickname));
        NickName = nickname;
    }
    
    public void SetEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
        
        if(!IsValidateEmail(email))
            throw new ArgumentException("Email is invalid", nameof(email));
        
        Email = email;
    }

    public void SetProfile(string nickname, string email)
    {
        SetNickName(nickname);
        SetEmail(email);
    }

    public void SetRefreshToken(string refreshToken, DateTime refreshTokenExpiresAt)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ArgumentException("refreshToken cannot be null or whitespace", nameof(refreshToken));
        if (refreshTokenExpiresAt < DateTime.UtcNow)
            throw new ArgumentException("refreshTokenExpiresAt must be in the future", nameof(refreshTokenExpiresAt));
        
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
    }

    public void ClearRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiresAt = default;
    }
    
    /// <summary>
    /// 이메일 주소의 유효성을 검사합니다.
    /// </summary>
    private static bool IsValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        return emailRegex.IsMatch(email);
    }
    
    // TODO : 추후 욕설 및 비속어 검증 로직 추가
    /// <summary>
    /// 닉네임의 유효성을 검사합니다. (한글 + 영문 + 숫자 + 언더스코어)
    /// </summary>
    private static bool IsValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return false;
        // (한글 + 영문 + 숫자 + 언더스코어)
        var nicknameRegex = new Regex(@"^[\uAC00-\uD7A3a-zA-Z0-9_]+$");
        return nicknameRegex.IsMatch(nickname);
    }


}