using GameServer.Application.Common;
using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Application.Interfaces;
using GameServer.Application.Services.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces;


namespace GameServer.Application.Services;

public class AuthService(IUserRepository userRepository, 
    IPasswordHasher passwordHasher,
    ISessionRepository sessionRepository, 
    IJwtTokenGenerator jwtTokenGenerator)
    : IAuthService
{
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
        return Result<RegisterResponse>.Success(new RegisterResponse(user.UserId, user.UserName, user.Email, user.CreatedAt));
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
        if(user is null)
            return Result<LoginResponse>.Failure(ErrorCodes.UserNotFound, ErrorMessages.UserNotFound);
        
        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if(!isValid)
            return Result<LoginResponse>.Failure(ErrorCodes.InvalidCredentials, ErrorMessages.InvalidCredentials);
        
        var accessToken = jwtTokenGenerator.GenerateAccessToken(user.UserId, user.UserName, user.Email);
        // 성공
        return Result<LoginResponse>.Success(new LoginResponse(user.UserId, user.UserName, user.Email, accessToken));
    }

    public Task LogoutAsync(string sessionId)
    {
        return sessionRepository.RemoveSessionAsync(sessionId);
    }

    public Task<bool> ValidateTokenAsync(string token)
    {
        throw new NotImplementedException();
    }
}