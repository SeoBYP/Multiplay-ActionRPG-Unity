using System.Security.Claims;
using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.Auth.Interfaces;
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
        var result = await authService.RegisterAsync(request.Password, request.Email, context.CancellationToken);
        
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
        var result = await authService.LoginAsync(request.Email, request.Password, request.DeviceId, context.CancellationToken);
        
        if (result.IsSuccess)
        {
            var response = new LoginResponse
            {
                Result = result.ToGrpcResult(),
                AccessToken = result.IsSuccess? result.Value!.AccessToken : null,
                RefreshToken = result.Value?.RefreshToken,
                SessionId = result.Value?.Session.SessionId,
                ExpiresAt = result.Value is not null ? new DateTimeOffset(result.Value.ExpiresAt).ToUnixTimeSeconds() : 0,
                User = result.Value?.User.ToUserInfo()
            };
            return response;
        }
        return new LoginResponse { Result = result.ToGrpcResult() };
    }

    [AllowAnonymous]
    public override async Task<RefreshResponse> Refresh(RefreshRequest request, ServerCallContext context)
    {
        var accessToken = context.GetAccessToken();

        if (string.IsNullOrWhiteSpace(accessToken))
            return new RefreshResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        
        var result = await authService.RefreshTokenAsync(accessToken, request.RefreshToken, request.DeviceId, context.CancellationToken);
        if (result.IsSuccess)
        {
            return new RefreshResponse
            {
                Result = result.ToGrpcResult(),
                AccessToken = result.Value?.AccessToken,
                RefreshToken = result.Value?.RefreshToken,
                SessionId = result.Value?.Session.SessionId,
                ExpiresAt = new DateTimeOffset(result.Value!.ExpiresAt).ToUnixTimeSeconds()
            };
        }

        return new RefreshResponse { Result = ResultExtensions.CreateFailureGrpcResult() };
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        
        var result = await authService.LogoutAsync(sessionId, context.CancellationToken);
        return new LogoutResponse
        {
            Result = result.ToGrpcResult(),
        };
    }
}