using GameServer.Application.Common;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;
using LoginRequest = GameServer.Application.DTOs.Auth.Login.LoginRequest;
using LoginResponse = GameServer.Application.DTOs.Auth.Login.LoginResponse;
using RegisterRequest = GameServer.Application.DTOs.Auth.Register.RegisterRequest;
using RegisterResponse = GameServer.Application.DTOs.Auth.Register.RegisterResponse;

namespace GameServer.Application.Services.Auth.Interfaces;

public interface IAuthService
{
    // Domain Entity 반환
    Task<Result<User>> RegisterAsync(string userName, string password, string email);
    
    // 복잡한 경우: 튜플 또는 별도 Result 객체
    Task<Result<LoginResult>> LoginAsync(string userName, string password);
    
    Task<Result> LogoutAsync(string sessionId);
    
    Task<bool> ValidateTokenAsync(string token);
}