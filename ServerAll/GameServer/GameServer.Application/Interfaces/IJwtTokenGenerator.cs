using System.Security.Claims;

namespace GameServer.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(long userId, string userName, string email);
    ValueTask<ClaimsPrincipal?> ValidateToken(string token, bool validateLifetime = true);
}