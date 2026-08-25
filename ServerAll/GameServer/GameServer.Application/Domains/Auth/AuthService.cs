using System.Security.Cryptography;
using GameServer.Application.Common;
using GameServer.Application.Domains.Account;
using GameServer.Application.Domains.Auth.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.Application.Domains.Auth;

public class AuthService(
    IAccountService accountService,
    IUserRepository userRepository,
    IUserCredentialRepository userCredentialRepository,
    IUserSessionRepository userSessionRepository,
    IUserProfileRepository userProfileRepository,
    IUserPositionService userPositionService,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger)
    : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<Result<LoginResult>> LoginAsync(string email, string password, string deviceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(deviceId))
        {
            var missingFields = new List<string>();
            if (string.IsNullOrWhiteSpace(email)) missingFields.Add("email");
            if (string.IsNullOrWhiteSpace(password)) missingFields.Add("password");
            if (string.IsNullOrWhiteSpace(deviceId)) missingFields.Add("deviceId");

            var message = $"{ErrorMessages.InvalidRequest} (Missing: {string.Join(", ", missingFields)})";
            logger.LogWarning("Login failed due to invalid request. Missing fields: {MissingFields}", missingFields);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, message);
        }

        var verifyResult = await accountService.VerifyCredentialAsync(email, password, ct);
        if (!verifyResult.IsSuccess)
        {
            logger.LogInformation("Login failed during credential verification for email {Email} with code {ErrorCode}", email, verifyResult.InternalErrorCode);
            return Result<LoginResult>.Failure(
                verifyResult.InternalErrorCode,
                verifyResult.Message ?? ErrorMessages.InvalidCredentials);
        }

        var user = await userRepository.GetByIdAsync(verifyResult.Value!.UserId, ct);
        if (user is null)
        {
            logger.LogError("Login failed because user {UserId} was not found after credential verification", verifyResult.Value.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        }

        var userSession = await userSessionRepository.CreateSessionAsync(user.UserId, ct);
        if (userSession is null)
        {
            logger.LogError("Login failed because session creation returned null for user {UserId}", user.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);
        }
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user.UserId, user.PublicId, email, userSession.SessionId);
        var expiresAt = _jwtOptions.GetExpirationTime();

        var credential = await userCredentialRepository.FindByIdAsync(user.UserId, ct);
        if (credential is null)
        {
            logger.LogError("Login failed because credential for user {UserId} was not found", user.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        }

        var refreshToken = jwtTokenGenerator.GenerateRefreshToken(credential.RefreshTokenVersion + 1);
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);
        var hashedRefreshToken = HashRefreshToken(refreshToken, deviceId);

        credential.SetRefreshToken(hashedRefreshToken, refreshTokenExpiry);
        await userCredentialRepository.UpdateAsync(credential, ct);
        // 새 로그인은 새 체인이다. 옛 세대 기록을 남겨 두면 오래된 토큰 하나로 갓 만든 세션이 끊길 수 있다.
        await userCredentialRepository.ClearPreviousRefreshTokenAsync(user.UserId, ct);

        logger.LogInformation("Login succeeded for user {UserId} with session {SessionId}", user.UserId, userSession.SessionId);

        return Result<LoginResult>.Success(new LoginResult(
            user,
            userSession,
            accessToken,
            refreshToken,
            expiresAt));
    }

    public async Task<Result> LogoutAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            logger.LogWarning("Logout failed because session id was empty");
            return Result.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is not null)
        {
            await userCredentialRepository.ClearRefreshTokenAsync(userSession.UserId, ct);
            await userCredentialRepository.ClearPreviousRefreshTokenAsync(userSession.UserId, ct);
            logger.LogInformation("Cleared refresh token during logout for user {UserId}", userSession.UserId);

            // Main 이탈 — 휘발(Redis) 위치를 DB 로 확정한다(B7). 실패해도 로그아웃을 막지 않는다.
            try
            {
                await userPositionService.FlushAsync(userSession.UserId, ct);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Logout: 위치 확정 실패 user {UserId} (로그아웃은 계속)", userSession.UserId);
            }
        }

        await userSessionRepository.RemoveSessionAsync(sessionId, ct);
        logger.LogInformation("Logout completed for session {SessionId}", sessionId);
        return Result.Success();
    }

    public async Task<Result<LoginResult>> RefreshTokenAsync(string accessToken, string refreshToken, string deviceId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(deviceId))
        {
            logger.LogWarning("Refresh failed because refresh token or device id was empty");
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(accessToken, false);
        if (claimsPrincipal is null)
        {
            logger.LogWarning("Refresh failed because access token validation failed");
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var sessionIdClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionIdClaim is null)
        {
            logger.LogWarning("Refresh failed because session id claim was missing");
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionIdClaim.Value, ct);
        if (userSession is null)
        {
            logger.LogInformation("Refresh failed because session {SessionId} was not found", sessionIdClaim.Value);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var credential = await userCredentialRepository.FindByIdAsync(userSession.UserId, ct);
        if (credential is null)
        {
            logger.LogInformation("Refresh failed because credential for user {UserId} was not found", userSession.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        if (credential.RefreshToken is null)
        {
            logger.LogWarning("Refresh failed because stored refresh token was missing for user {UserId}", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // 소유 증명이 먼저다. 이 검사를 통과하지 못한 요청으로는 아무것도 파괴하지 않는다.
        // (검사보다 파괴가 앞서면, 유출된 accessToken 하나로 아무 문자열이나 던져 세션을 끊는 DoS 가 된다.)
        var hashedInputToken = HashRefreshToken(refreshToken, deviceId);
        var matchesCurrent = FixedTimeEqualsHex(credential.RefreshToken, hashedInputToken);

        if (!matchesCurrent)
        {
            var previous = await userCredentialRepository.GetPreviousRefreshTokenAsync(credential.UserId, ct);
            if (previous is null || !FixedTimeEqualsHex(previous.HashedToken, hashedInputToken))
            {
                // 우리가 발급한 토큰이라는 증명이 없다. 실패만 반환하고 세션은 그대로 둔다.
                logger.LogWarning("Refresh failed because refresh token did not match any issued token for user {UserId}", credential.UserId);
                return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            }

            var elapsedSinceRotation = DateTime.UtcNow - previous.RotatedAt;
            if (elapsedSinceRotation >= TimeSpan.FromSeconds(_jwtOptions.RefreshReuseGraceSeconds))
            {
                // 유예를 넘겨 제출된 구세대 토큰 = 탈취 확정. 여기서만 전부 무효화한다.
                await userCredentialRepository.ClearRefreshTokenAsync(credential.UserId, ct);
                await userCredentialRepository.ClearPreviousRefreshTokenAsync(credential.UserId, ct);
                await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct);
                logger.LogWarning("Refresh token reuse detected for user {UserId}. Rotated {ElapsedSeconds}s ago",
                    credential.UserId, (int)elapsedSinceRotation.TotalSeconds);
                return Result<LoginResult>.Failure(ErrorCodes.TokenReuseDetected, ErrorMessages.TokenReuseDetected);
            }

            // 회전 직후의 구세대 토큰 = 응답을 못 받은 클라이언트의 재시도. 탈취로 오판하면 대량 로그아웃이 된다.
            logger.LogInformation("Refresh accepted previous-generation token as a retry for user {UserId} ({ElapsedSeconds}s after rotation)",
                credential.UserId, (int)elapsedSinceRotation.TotalSeconds);
        }

        if (credential.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            logger.LogInformation("Refresh failed because refresh token expired for user {UserId}", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.SessionExpired, ErrorMessages.SessionExpired);
        }

        var user = await userRepository.GetByIdAsync(credential.UserId, ct);
        if (user is null)
        {
            logger.LogInformation("Refresh failed because user {UserId} was not found", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }
        
        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(user.UserId, user.PublicId, credential.Email, userSession.SessionId);
        var accessTokenExpiresAt = _jwtOptions.GetExpirationTime();

        var newRawRefreshToken = jwtTokenGenerator.GenerateRefreshToken(credential.RefreshTokenVersion + 1);
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);
        var hashedNewRefreshToken = HashRefreshToken(newRawRefreshToken, deviceId);

        credential.SetRefreshToken(hashedNewRefreshToken, refreshTokenExpiry);
        await userCredentialRepository.UpdateAsync(credential, ct);

        // 물러난 토큰을 재사용 탐지용으로 남긴다. 재시도로 들어온 경우에는 갱신하지 않는다 —
        // 갱신하면 같은 토큰을 유예 안에서 계속 되밀어 탐지를 무한히 미룰 수 있다.
        if (matchesCurrent)
        {
            await userCredentialRepository.SetPreviousRefreshTokenAsync(
                credential.UserId,
                hashedInputToken,
                DateTime.UtcNow,
                TimeSpan.FromHours(_jwtOptions.RefreshTokenExpirationHours),
                ct);
        }

        logger.LogInformation("Refresh succeeded for user {UserId} with session {SessionId}", user.UserId, userSession.SessionId);

        return Result<LoginResult>.Success(new LoginResult(
            user,
            userSession,
            newAccessToken,
            newRawRefreshToken,
            accessTokenExpiresAt));
    }

    private static bool FixedTimeEqualsHex(string storedHash, string candidateHash)
    {
        if (storedHash.Length != candidateHash.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(storedHash),
            Convert.FromHexString(candidateHash));
    }

    private static string HashRefreshToken(string refreshToken, string deviceId)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken + deviceId));
        return Convert.ToHexString(bytes);
    }

    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("ValidateToken failed because token was empty");
            return false;
        }

        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(token);
        if (claimsPrincipal is null)
        {
            logger.LogInformation("ValidateToken failed because token validation returned null");
            return false;
        }

        var sessionId = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
        {
            logger.LogWarning("ValidateToken failed because sid claim was missing");
            return false;
        }

        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId.Value, ct);
        if (userSession is null)
        {
            logger.LogInformation("ValidateToken failed because session {SessionId} was not found", sessionId.Value);
            return false;
        }

        // 인증된 활동 = 생존 신호. 리퍼가 "아직 사람이 있다"를 판단할 유일한 근거라
        // 검증 성공 지점에서 갱신한다(저장소가 스로틀하므로 요청마다 쓰지는 않는다).
        await userSessionRepository.TouchSessionAsync(sessionId.Value, ct);

        logger.LogDebug("ValidateToken succeeded for session {SessionId}", sessionId.Value);
        return true;
    }
}
