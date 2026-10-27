using System.Security.Claims;
using System.Text;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Infrastructure.Security;

public class JwtTokenGenerator(IOptions<JwtOptions> jwtOptions) : IJwtTokenGenerator
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly JsonWebTokenHandler _jwtHandler = new();

    public string GenerateAccessToken(long userId, string publicId, string email, string sessionId)
    {
        var claim = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, publicId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Sid, sessionId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var description = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Subject = new ClaimsIdentity(claim),
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            SigningCredentials = creds
        };
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

    public string GenerateRefreshToken(int version)
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return $"{Convert.ToBase64String(randomBytes)}.{version}";
    }
}
