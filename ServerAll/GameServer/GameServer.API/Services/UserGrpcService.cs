using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Services.User.Interfaces;
using GameServer.Grpc.Auth;
using GameServer.Grpc.User;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Services;

public class UserGrpcService(IUserService userService) : UserService.UserServiceBase
{
    public override async Task<SetNicknameResponse> SetNickName(SetNicknameRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        if (sessionId is null) 
            return new SetNicknameResponse
            {
                Result = ResultExtensions.CreateUnauthorizedGrpcResult()
            };

        var result = await userService.SetNicknameAsync(sessionId,request.Nickname);
        if (result.IsSuccess)
        {
            return new SetNicknameResponse
            {
                Result = result.ToGrpcResult(),
                User = result.Value?.ToUserInfo()
            };
        }
        return new SetNicknameResponse { Result = result.ToGrpcResult() };
    }
}