using System.Security.Claims;

namespace GameServer.Application.Security.Interface;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(long userId, string publicId, string email, string sessionId);
    ValueTask<ClaimsPrincipal?> ValidateToken(string token, bool validateLifetime = true);
    string GenerateRefreshToken();
    
}
