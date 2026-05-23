using GameServer.API.Extension;
using GameServer.API.Extensions;
using GameServer.Application.Domains.Account;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Grpc.Auth;
using GameServer.Grpc.User;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using AuthService = GameServer.Grpc.Auth.AuthService;
using RegisterResponse = GameServer.Grpc.Auth.RegisterResponse;

namespace GameServer.API.Services;

public class AuthGrpcService(
    IAccountService accountService,
    IAuthService authService,
    IDungeonRoomRepository roomRepository,
    ILogger<AuthGrpcService> logger) : AuthService.AuthServiceBase
{
    [AllowAnonymous]
    public override async Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        logger.LogInformation("Register request received for email {Email}", request.Email);
        var result = await accountService.RegisterAsync(request.Email, request.Password, context.CancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation("Register request succeeded for email {Email}", request.Email);
            return new RegisterResponse
            {
                Result = result.ToGrpcResult(),
                User = result.Value?.ToUserInfo(),
            };
        }

        logger.LogWarning("Register request failed for email {Email} with code {ErrorCode}", request.Email, result.InternalErrorCode);
        return new RegisterResponse { Result = result.ToGrpcResult() };
    }

    [AllowAnonymous]
    public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
    {
        logger.LogInformation("Login request received for email {Email}", request.Email);
        var result = await authService.LoginAsync(request.Email, request.Password, request.DeviceId, context.CancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation("Login request succeeded for email {Email} with session {SessionId}", request.Email, result.Value?.Session.SessionId);
            var room   = await roomRepository.GetByUserIdAsync(result.Value!.User.UserId, context.CancellationToken);
            var roomId = room?.RoomId ?? 0L;
            return new LoginResponse
            {
                Result = result.ToGrpcResult(),
                AccessToken = result.Value?.AccessToken,
                RefreshToken = result.Value?.RefreshToken,
                SessionId = result.Value?.Session.SessionId,
                ExpiresAt = result.Value is not null ? new DateTimeOffset(result.Value.ExpiresAt).ToUnixTimeSeconds() : 0,
                User = result.Value?.User.ToUserInfo(roomId)
            };
        }

        logger.LogWarning("Login request failed for email {Email} with code {ErrorCode}", request.Email, result.InternalErrorCode);
        return new LoginResponse { Result = result.ToGrpcResult() };
    }

    [AllowAnonymous]
    public override async Task<RefreshResponse> Refresh(RefreshRequest request, ServerCallContext context)
    {
        var accessToken = context.GetAccessToken();
        logger.LogInformation("Refresh request received");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logger.LogWarning("Refresh request failed because access token was missing");
            return new RefreshResponse { Result = ResultExtensions.CreateUnauthorizedGrpcResult() };
        }

        var result = await authService.RefreshTokenAsync(accessToken, request.RefreshToken, request.DeviceId, context.CancellationToken);
        if (result.IsSuccess)
        {
            logger.LogInformation("Refresh request succeeded with session {SessionId}", result.Value?.Session.SessionId);
            return new RefreshResponse
            {
                Result = result.ToGrpcResult(),
                AccessToken = result.Value?.AccessToken,
                RefreshToken = result.Value?.RefreshToken,
                SessionId = result.Value?.Session.SessionId,
                ExpiresAt = new DateTimeOffset(result.Value!.ExpiresAt).ToUnixTimeSeconds()
            };
        }

        logger.LogWarning("Refresh request failed with code {ErrorCode}", result.InternalErrorCode);
        return new RefreshResponse { Result = ResultExtensions.CreateFailureGrpcResult() };
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var sessionId = context.GetSessionId();
        logger.LogInformation("Logout request received for session {SessionId}", sessionId);

        var result = await authService.LogoutAsync(sessionId, context.CancellationToken);
        if (result.IsSuccess)
            logger.LogInformation("Logout request succeeded for session {SessionId}", sessionId);
        else
            logger.LogWarning("Logout request failed for session {SessionId} with code {ErrorCode}", sessionId, result.InternalErrorCode);

        return new LogoutResponse
        {
            Result = result.ToGrpcResult(),
        };
    }
}
