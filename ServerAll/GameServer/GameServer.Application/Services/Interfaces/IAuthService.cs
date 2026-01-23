using GameServer.Application.Common;
using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;

namespace GameServer.Application.Services.Interfaces;

public interface IAuthService
{
    public Task<Result<RegisterResponse>> RegisterAsync(RegisterRequest request);
    
    public Task<Result<LoginResponse>> LoginAsync(LoginRequest request);
    
    Task<Result> LogoutAsync(string sessionId);
    
    Task<bool> ValidateTokenAsync(string token);
}