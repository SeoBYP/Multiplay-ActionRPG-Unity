using GameServer.Application.DTOs.Auth.Login;
using GameServer.Application.DTOs.Auth.Register;
using GameServer.Application.Services.Auth.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GameServer.API.Controllers;


[ApiController]
[Route("api/v1/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpGet("test-error")]
    public IActionResult TestError()
    {
        throw new InvalidOperationException("테스트 예외입니다");
    }
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await authService.RegisterAsync(
            request.UserName, 
            request.Password, 
            request.Email);
    
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Message });
    
        // Domain → DTO 변환
        if (result.Value != null)
        {
            var response = new RegisterResponse(
                result.Value.UserId,
                result.Value.UserName,
                result.Value.Email,
                result.Value.CreatedAt);
    
            return Ok(response);
        }
        return Ok();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(
            request.UserName, 
            request.Password);
    
        if (!result.IsSuccess)
            return BadRequest(new { error = result.Message });
    
        var loginResult = result.Value;
    
        // Domain → DTO 변환
        if (loginResult != null)
        {
            var response = new LoginResponse(
                loginResult.User.UserId,
                loginResult.User.UserName,
                loginResult.User.Email,
                loginResult.AccessToken,
                loginResult.Session.SessionId,
                loginResult.ExpiresAt);  // 또는 JwtOptions에서 계산
    
            return Ok(response);
        }
        return Ok();
    }

    // TODO : SwaggerUI로 테스트 용, 추후에는 JWT로 통신 예정
    [HttpPost("logout")]
    // [Authorize]
    public async Task<IActionResult> Logout([FromBody] string sessionId)
    {
        // var sessionId = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;
        var result = await authService.LogoutAsync(sessionId);
        return Ok(result);
    }
}