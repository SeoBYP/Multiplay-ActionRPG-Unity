using System.Security.Claims;

namespace GameServer.Infrastructure.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(long userId, string nickName, string email, string sessionId);
    ValueTask<ClaimsPrincipal?> ValidateToken(string token, bool validateLifetime = true);
}