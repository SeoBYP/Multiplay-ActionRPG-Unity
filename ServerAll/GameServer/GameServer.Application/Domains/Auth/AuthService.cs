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
            logger.LogInformation("Cleared refresh token during logout for user {UserId}", userSession.UserId);
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
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct);
            logger.LogWarning("Refresh failed because stored refresh token was missing for user {UserId}", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // Reuse Detection: Extract version from token
        var tokenParts = refreshToken.Split('.');
        if (tokenParts.Length != 2 || !int.TryParse(tokenParts[1], out var submittedVersion))
        {
            logger.LogWarning("Refresh failed because refresh token format is invalid for user {UserId}", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        if (submittedVersion < credential.RefreshTokenVersion)
        {
            // Already rotated token -> Theft detected
            await userCredentialRepository.ClearRefreshTokenAsync(credential.UserId, ct);
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct);
            logger.LogWarning("Refresh token reuse detected for user {UserId}. Submitted version: {Submitted}, Current: {Current}",
                credential.UserId, submittedVersion, credential.RefreshTokenVersion);
            return Result<LoginResult>.Failure(ErrorCodes.TokenReuseDetected, ErrorMessages.TokenReuseDetected);
        }

        var hashedInputToken = HashRefreshToken(refreshToken, deviceId);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(credential.RefreshToken),
                Convert.FromHexString(hashedInputToken)))
        {
            await userCredentialRepository.ClearRefreshTokenAsync(credential.UserId, ct);
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct);
            logger.LogWarning("Refresh failed because refresh token validation failed (hash mismatch) for user {UserId}", credential.UserId);
            return Result<LoginResult>.Failure(ErrorCodes.SessionExpired, ErrorMessages.SessionExpired);
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
        logger.LogInformation("Refresh succeeded for user {UserId} with session {SessionId}", user.UserId, userSession.SessionId);

        return Result<LoginResult>.Success(new LoginResult(
            user,
            userSession,
            newAccessToken,
            newRawRefreshToken,
            accessTokenExpiresAt));
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

        logger.LogDebug("ValidateToken succeeded for session {SessionId}", sessionId.Value);
        return true;
    }
}
