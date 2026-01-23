using GameServer.Application.DTOs.Requests;
using GameServer.Application.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Register), new { id = response.Value }, response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            return Conflict(new { error = ex.Message });  // 409
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });  // 400
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (NullReferenceException)
        {
            return Unauthorized(new { error = "Invalid username or password" });  // 401
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { error = "Invalid username or password" });  // 401
        }
    }
}