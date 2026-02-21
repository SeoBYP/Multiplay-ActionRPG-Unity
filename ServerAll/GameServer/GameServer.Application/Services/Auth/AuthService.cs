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
    
    public async Task<Result<User>> RegisterAsync(string password, string email)
    {
        if(string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            return Result<User>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        
        // 이메일 중복 체크
        var existingEmail = await userRepository.IsEmailExistsAsync(email);
        if (existingEmail)
            return Result<User>.Failure(ErrorCodes.EmailAlreadyTaken, ErrorMessages.EmailAlreadyTaken);
        
        // 비밀번호 해싱
        var hash = passwordHasher.HashPassword(password);

        // User Entity 생성
        var user = await userRepository.AddAsync(hash, email);
        
        // Response
        return Result<User>.Success(user);
    }

    public async Task<Result<LoginResult>> LoginAsync(string email, string password)
    {
        // 잘못된 요청
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<LoginResult>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // User 조회: 먼저 닉네임으로, 없으면 이메일로 재시도
        var user = await userRepository.GetByEmailAsync(email);
        if (user is null)
            return Result<LoginResult>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);

        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isValid)
            return Result<LoginResult>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);

        // 세션 생성
        var userSession = await userSessionRepository.CreateSessionAsync(user.UserId, user.NickName, user.Email, user.PublicId);
        if (userSession is null)
            return Result<LoginResult>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);

        var accessToken =
            jwtTokenGenerator.GenerateAccessToken(user.UserId, user.NickName, user.Email, userSession.SessionId);
        var expiresAt = _jwtOptions.GetExpirationTime();
        // 성공
        return Result<LoginResult>.Success(new LoginResult(
            user, 
            userSession, 
            accessToken,
            expiresAt));
    }

    public async Task<Result> LogoutAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Failure(ErrorCodes.InvalidRequest,
                ErrorMessages.InvalidRequest);

        await userSessionRepository.RemoveSessionAsync(sessionId);
        return Result.Success();
    }

    public async Task<bool> ValidateTokenAsync(string token)
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
        var userSession = await userSessionRepository.GetBySessionIdAsync(sessionId.Value);
        if (userSession is null)
            return false;
        
        return true;
    }
}