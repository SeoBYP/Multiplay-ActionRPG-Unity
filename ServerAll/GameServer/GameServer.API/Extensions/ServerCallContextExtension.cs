using System.Security.Claims;
using GameServer.API.Services;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;

namespace GameServer.API.Extensions;

public static class ServerCallContextExtension
{
    public static string? GetSessionId(this ServerCallContext context)
    {
        return context.GetHttpContext().User.FindFirstValue(JwtRegisteredClaimNames.Sid);
    }

    public static long? GetUserId(this ServerCallContext context)
        => ResolveUserId(context.GetHttpContext().User);

    /// <summary>'sub' 가 MapInboundClaims 로 NameIdentifier 에 리매핑될 수 있어 두 클레임을 모두 확인한다. 순수 함수.</summary>
    public static long? ResolveUserId(ClaimsPrincipal user)
    {
        if (user is null)
            return null;

        var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var userId) ? userId : null;
    }

    public static string? GetAccessToken(this ServerCallContext context)
    {
        var authHeader = context.RequestHeaders.GetValue("authorization");

        if (string.IsNullOrEmpty(authHeader))
        {
            Log.Warning("Authorization header is missing");
            return null;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("Invalid authorization header format: {AuthorizationHeader}", authHeader);
            return null;
        }

        return authHeader.Substring("Bearer ".Length).Trim();
    }
}
