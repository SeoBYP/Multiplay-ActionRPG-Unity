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
    {
        var sub = context.GetHttpContext().User.FindFirstValue(JwtRegisteredClaimNames.Sub);
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
