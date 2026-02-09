using System.Security.Claims;
using GameServer.API.Extensions;
using GameServer.Application.Common;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Grpc.Auth;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using AuthService = GameServer.Grpc.Auth.AuthService;
using RegisterResponse = GameServer.Grpc.Auth.RegisterResponse;
using Result = GameServer.Grpc.Common.Result;

namespace GameServer.API.Services;

public class AuthGrpcService(IAuthService authService) : AuthService.AuthServiceBase
{
    [AllowAnonymous]
    public override async Task<RegisterResponse> Register(RegisterRequest request,
        ServerCallContext context)
    {
        var result = await authService.RegisterAsync(request.UserName, request.Password, request.Email);

        return new RegisterResponse
        {
            Result = result.ToGrpcResult(),
            User = result.IsSuccess
                ? new UserInfo
                {
                    UserId = result.Value!.UserId,
                    UserName = result.Value!.UserName,
                    Email = result.Value.Email,
                    CreatedAt = new DateTimeOffset(result.Value!.CreatedAt).ToUnixTimeSeconds()
                }
                : null
        };
    }

    [AllowAnonymous]
    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var result = await authService.LoginAsync(request.UserName, request.Password);

        return new LoginResponse
        {
            Result = result.ToGrpcResult(),
            AccessToken = result.IsSuccess ? result.Value!.AccessToken : null,
            SessionId = result.Value?.Session.SessionId,
            User = result.IsSuccess
                ? new UserInfo
                {
                    UserId = result.Value!.User.UserId,
                    UserName = result.Value!.User.UserName,
                    Email = result.Value.User.Email,
                    CreatedAt = new DateTimeOffset(result.Value!.User.CreatedAt).ToUnixTimeSeconds()
                }
                : null
        };
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var httpContext = context.GetHttpContext();
        var sessionId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (sessionId is null) 
            return new LogoutResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        
        var result = await authService.LogoutAsync(sessionId);
        return new LogoutResponse
        {
            Result = result.ToGrpcResult(),
        };
    }
}