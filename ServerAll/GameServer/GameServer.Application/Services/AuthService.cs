using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;
using GameServer.Application.Interfaces;
using GameServer.Application.Services.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces;


namespace GameServer.Application.Services;

public class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    : IAuthService
{
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        // 중복 체크
        var existing = await userRepository.GetByUsernameAsync(request.UserName);
        if (existing is not null)
            throw new InvalidOperationException("Username already exists");

        // 비밀번호 해싱
        var hash = passwordHasher.HashPassword(request.Password);

        // User Entity 생성
        var user = User.Create(request.UserName, hash, request.Email);

        // Add
        await userRepository.AddAsync(user);

        // Response
        return new RegisterResponse(user.UserId,
            user.UserName,
            user.Email,
            user.CreatedAt
            );
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        // User 조회
        var user = await userRepository.GetByUsernameAsync(request.UserName);
        if(user is null)
            throw new UnauthorizedAccessException("User not found");
        
        // 비밀번호 검증
        var isValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash);
        if(!isValid)
            throw new InvalidOperationException("Invalid password");
        
        // 성공
        return new LoginResponse(user.UserId, user.UserName, user.Email);
    }
}