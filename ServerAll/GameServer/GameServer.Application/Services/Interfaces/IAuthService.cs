using GameServer.Application.DTOs.Requests;
using GameServer.Application.DTOs.Responses;

namespace GameServer.Application.Services.Interfaces;

public interface IAuthService
{
    public Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    
    public Task<LoginResponse> LoginAsync(LoginRequest request);
}