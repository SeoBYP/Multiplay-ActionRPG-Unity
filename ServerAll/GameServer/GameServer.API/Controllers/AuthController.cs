using GameServer.Application.DTOs.Requests;
using GameServer.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Controllers;


[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    
    [HttpGet("test-error")]
    public IActionResult TestError()
    {
        throw new InvalidOperationException("테스트 예외입니다");
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return Ok(result.Value);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(result.Value);  // LoginResponse만 반환
    }

    // TODO : SwaggerUI로 테스트 용, 추후에는 JWT로 통신 예정
    [HttpPost("logout")]
    // [Authorize]
    public async Task<IActionResult> Logout([FromBody] string sessionId)
    {
        // var sessionId = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
        var result = await _authService.LogoutAsync(sessionId);
        return Ok(result);
    }
}