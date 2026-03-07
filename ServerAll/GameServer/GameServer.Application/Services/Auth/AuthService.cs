using GameServer.Application.Common;
using GameServer.Application.Services.Auth.Interfaces;

using GameServer.Infrastructure.Interfaces;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.Application.Services.Auth;

using User = Domain.Entities.User.User;

public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUserSessionRepository userSessionRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<JwtOptions> jwtOptions)
    : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    
    public async Task<Result<User>> RegisterAsync(string password, string email, CancellationToken ct = default)
    {
        if(string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        // 이메일 중복 체크
        var existingEmail = await userRepository.IsEmailExistsAsync(email, ct);
        if (existingEmail)
            return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
        
        // 비밀번호 해싱
        var hash = passwordHasher.HashPassword(password);

        // User Entity 생성
        var user = await userRepository.AddAsync(hash, email, ct);
        
        // Response
        return Result<User>.Success(user);
    }

    public async Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        // 잘못된 요청
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // User 조회: 먼저 닉네임으로, 없으면 이메일로 재시도
        var user = await userRepository.GetByEmailAsync(email, ct);
        if (user is null)
            return Result<LoginResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);

        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);

        // 세션 생성
        var userSession = await userSessionRepository.CreateSessionAsync(user.UserId, user.NickName, user.Email, user.PublicId, ct);
        if (userSession is null)
            return Result<LoginResult>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);

        var accessToken =
            jwtTokenGenerator.GenerateAccessToken(user.UserId, user.NickName, user.Email, userSession.SessionId);
        var expiresAt = _jwtOptions.GetExpirationTime();

        var refreshToken = jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);
        
        // 유저 정보에 리프레시 토큰 저장
        var hashedRefreshToken = HashRefreshToken(refreshToken);
        await userRepository.UpdateRefreshTokenAsync(user.UserId, hashedRefreshToken, refreshTokenExpiry, ct);

        // 성공
        return Result<LoginResult>.Success(new LoginResult(
            user, 
            userSession, 
            accessToken,
            expiresAt));
    }

    public async Task<Result> LogoutAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Failure(ErrorCodes.InvalidRequest,
                ErrorMessages.InvalidRequest);

        // 로그아웃 시 리프레시 토큰도 삭제 (세션 정보를 통해 유저 ID를 알아내야 함)
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId, ct);
        if (userSession is not null)
        {
            await userRepository.ClearRefreshTokenAsync(userSession.UserId, ct);
        }

        await userSessionRepository.RemoveSessionAsync(sessionId, ct);
        return Result.Success();
    }

    public async Task<Result<LoginResult>> RefreshTokenAsync(string accessToken, CancellationToken ct = default)
    {
        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(accessToken, false);
        if (claimsPrincipal is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        var sessionIdClaim = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionIdClaim is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
            
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionIdClaim.Value, ct);
        if (userSession is null)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        // 3. UserId → User 조회 → RefreshToken 꺼내기
        var user = await userRepository.GetByIdAsync(userSession.UserId, ct);
        if (user is null)
        {
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // 4. RefreshToken null 체크
        if (user.RefreshToken is null)
        {
            await userSessionRepository.RemoveSessionAsync(userSession.SessionId, ct); // 세션 강제 종료
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // 4. RefreshToken 만료일 검사
        if (user.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // 5. 새 AccessToken 발급 + RefreshToken Rotation
        var newAccessToken = jwtTokenGenerator.GenerateAccessToken(user.UserId, user.NickName, user.Email, userSession.SessionId);
        var accessTokenExpiresAt = _jwtOptions.GetExpirationTime();

        var newRawRefreshToken = jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddHours(_jwtOptions.RefreshTokenExpirationHours);

        // 로테이션된 토큰 저장
        var hashedNewRefreshToken = HashRefreshToken(newRawRefreshToken);
        await userRepository.UpdateRefreshTokenAsync(user.UserId, hashedNewRefreshToken, refreshTokenExpiry, ct);

        return Result<LoginResult>.Success(new LoginResult(
            user, 
            userSession, 
            newAccessToken, 
            accessTokenExpiresAt));
    }

    private static string HashRefreshToken(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }


    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        // 토큰이 유효한 값인지 검증
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // 토큰 검증
        var claimsPrincipal = await jwtTokenGenerator.ValidateToken(token);
        if (claimsPrincipal is null)
            return false;

        // sessionID 가져오기
        var sessionId = claimsPrincipal.FindFirst(JwtRegisteredClaimNames.Sid);
        if (sessionId is null)
            return false;

        // 현재 session이 활성화 되었는지 
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId.Value, ct);
        if (userSession is null)
            return false;
        
        return true;
    }
}