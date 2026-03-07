using System.Security.Claims;
using GameServer.API.Services;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Extensions;

public static class ServerCallContextExtension
{
    public static string? GetSessionId(this ServerCallContext context)
    {
        return context.GetHttpContext().User.FindFirstValue(JwtRegisteredClaimNames.Sid);
    }

    public static string? GetAccessToken(this ServerCallContext context)
    {
        var authHeader = context.RequestHeaders.GetValue("authorization");

        if (string.IsNullOrEmpty(authHeader))
        {
            Console.WriteLine("Authorization 헤더 없음");
            return null;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"잘못된 Authorization 형식: {authHeader}");
            return null;
        }

        return authHeader.Substring("Bearer ".Length).Trim();
    }
}