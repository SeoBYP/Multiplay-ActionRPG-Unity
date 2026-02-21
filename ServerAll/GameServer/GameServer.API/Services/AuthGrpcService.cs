using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Services.Auth.Interfaces;
using GameServer.Grpc.Auth;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using AuthService = GameServer.Grpc.Auth.AuthService;
using RegisterResponse = GameServer.Grpc.Auth.RegisterResponse;
using GameServer.Grpc.User;

namespace GameServer.API.Services;

public class AuthGrpcService(IAuthService authService) : AuthService.AuthServiceBase
{
    [AllowAnonymous]
    public override async Task<RegisterResponse> Register(RegisterRequest request,
        ServerCallContext context)
    {
        var result = await authService.RegisterAsync(request.Password, request.Email);
        
        if (result.IsSuccess)
        {
            var response = new RegisterResponse
            {
                Result = result.ToGrpcResult(),
                User = result.Value?.ToUserInfo(),
            };
            return response;
        }

        return new RegisterResponse { Result = result.ToGrpcResult() };
    }

    [AllowAnonymous]
    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var result = await authService.LoginAsync(request.Email, request.Password);
        
        if (result.IsSuccess)
        {
            var response = new LoginResponse
            {
                Result = result.ToGrpcResult(),
                AccessToken = result.IsSuccess? result.Value!.AccessToken : null,
                SessionId = result.Value?.Session.SessionId,
                User = result.Value?.User.ToUserInfo()
            };
            return response;
        }
        return new LoginResponse { Result = result.ToGrpcResult() };
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