using GameServer.Application.Common;
using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Infrastructure.Interfaces;
using GameServer.Application.Services.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces;
using GameServer.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ISessionRepository sessionRepository,
    IJwtTokenGenerator jwtTokenGenerator,
    IOptions<JwtOptions> jwtOptions)
    : IAuthService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    
    public async Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request)
    {
        // 중복 체크
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Result<RegisterResponse>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        var existing = await userRepository.GetByUsernameAsync(request.UserName);
        if (existing is not null)
            return Result<RegisterResponse>.Failure(ErrorCodes.UserAlreadyExists, ErrorMessages.UserAlreadyExists);

        // 비밀번호 해싱
        var hash = passwordHasher.HashPassword(request.Password);

        // User Entity 생성
        var user = User.Create(request.UserName, hash, request.Email);

        // Add
        await userRepository.AddAsync(user);

        // Response
        return Result<RegisterResponse>.Success(new RegisterResponse(user.UserId, user.UserName, user.Email,
            user.CreatedAt));
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request)
    {
        // 잘못된 요청
        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return Result<LoginResponse>.Failure(ErrorCodes.InvalidRequest, ErrorMessages.InvalidRequest);
        }

        // User 조회
        var user = await userRepository.GetByUsernameAsync(request.UserName);
        if (user is null)
            return Result<LoginResponse>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);

        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if (!isValid)
            return Result<LoginResponse>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);

        // 세션 생성
        var userSession = await sessionRepository.CreateSessionAsync(user.UserId, user.UserName);
        if (userSession is null)
            return Result<LoginResponse>.Failure(ErrorCodes.InternalServerError, ErrorMessages.InternalServerError);

        var accessToken =
            jwtTokenGenerator.GenerateAccessToken(user.UserId, user.UserName, user.Email, userSession.SessionId);

        // 성공
        return Result<LoginResponse>.Success(new LoginResponse(
            user.UserId, 
            user.UserName,
            user.Email, 
            accessToken,
            userSession.SessionId, 
            _jwtOptions.GetExpirationTime()));
    }

    public async Task<Result> LogoutAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return Result.Failure(ErrorCodes.InvalidRequest,
                ErrorMessages.InvalidRequest);

        await sessionRepository.RemoveSessionAsync(sessionId);
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
        var userSession = await sessionRepository.GetBySessionIdAsync(sessionId.Value);
        if (userSession is null)
            return false;
        return true;
    }
}