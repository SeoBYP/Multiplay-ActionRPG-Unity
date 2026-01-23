using System.Security.Claims;
using System.Text;
using GameServer.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Infrastructure.Security;

public class JwtTokenGenerator(IOptions<JwtOptions> jwtOptions) : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly JsonWebTokenHandler _jwtHandler = new();

    public string GenerateAccessToken(long userId, string userName, string email, string sessionId)
    {
        var claim = new List<Claim>
        {
            // Sub(Subject) : 토큰의 주인
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            // 사용자 이름
            new Claim(JwtRegisteredClaimNames.UniqueName, userName),
            // 이메일
            new Claim(JwtRegisteredClaimNames.Email, email),
            // SessionID
            new Claim(JwtRegisteredClaimNames.Sid, sessionId),
            // jti(JWT ID) : 토큰 식별자, 토큰마다의 고유한 값,로그아웃/블랙리스트 같은 처리에 사용
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        // 서명용 비밀키 생성. 서버만 알고 있는 _jwtOptions.Secret을 바이트 배열로 만들어 키 객체로 변환합니다.
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        // 어떤 알고리즘으로 서명할지 지정합니다.
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 토큰의 “내용물”을 정의하는 객체
        var description = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer, //발급자(서버 이름)
            Audience = _jwtOptions.Audience, // 수신자(클라이언트/서비스)
            Subject = new ClaimsIdentity(claim), // 위에서 만든 클레임 묶음
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes), // 만료시간
            SigningCredentials = creds // 서명 정보(키 + 알고리즘)
        };
        // JWT 토큰 생성
        return _jwtHandler.CreateToken(description);
    }

    public async ValueTask<ClaimsPrincipal?> ValidateToken(string token, bool validateLifetime = true)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _jwtOptions.Issuer,
            ValidAudience = _jwtOptions.Audience,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = validateLifetime,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        var result = await _jwtHandler.ValidateTokenAsync(token, parameters);

        if (!result.IsValid)
            return null;
        if (result.ClaimsIdentity == null)
            return null;
        return new ClaimsPrincipal(result.ClaimsIdentity);
    }
}