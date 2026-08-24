namespace GameServer.Application.Security;

public class JwtOptions
{
    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string Secret { get; init; } = "";
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenExpirationHours { get; init; } = 24 * 7; // 기본 7일 (168시간)

    /// <summary>
    /// 회전 직후 이전 세대 리프레시 토큰을 "네트워크 재시도"로 받아 주는 유예 시간(초).
    /// 이 시간이 지난 뒤 제출된 이전 세대 토큰은 탈취로 간주한다.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; init; } = 60;
    
    public TimeSpan AccessTokenExpiration => TimeSpan.FromMinutes(AccessTokenMinutes);
    public DateTime GetExpirationTime() => DateTime.UtcNow.Add(AccessTokenExpiration);
}