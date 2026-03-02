using System.Security.Claims;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Extensions;

public static class ServerCallContextExtension
{
    public static string? GetSessionId(this ServerCallContext context)
    {
        return context.GetHttpContext().User.FindFirstValue(JwtRegisteredClaimNames.Sid);
    }
}