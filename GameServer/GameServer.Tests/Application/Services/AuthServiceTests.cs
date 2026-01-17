using GameServer.Application.DTOs.Requests;
using GameServer.Application.Interfaces;
using GameServer.Application.Services;
using GameServer.Domain.Interfaces;
using GameServer.Infrastructure.Repositories;
using GameServer.Infrastructure.Security;

namespace GameServer.Tests.Application.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthService _authService;

    public AuthServiceTests() // ← 생성자 사용
    {
        _userRepository = new InMemoryUserRepository();
        _passwordHasher = new PasswordHasher();
        _authService = new AuthService(_userRepository, _passwordHasher);
    }

    [Fact]
    public async Task RegisterAsync_는_새로운_User를_생성한다()
    {
        // given
        var request = new RegisterRequest("testuser", "password123", "test@example.com");

        // wher
        var response = await _authService.RegisterAsync(request);

        // then
        Assert.NotNull(response);
        Assert.True(response.UserId > 0);
        Assert.Equal(request.UserName, response.UserName);
        Assert.Equal(request.Email, response.Email);

        var savedUser = await _userRepository.GetByUsernameAsync(request.UserName);
        Assert.NotNull(savedUser);
        Assert.Equal(request.UserName, savedUser.UserName);
    }


    [Fact]
    public async Task RegisterAsync_는_중복_Username이면_예외를_던진다()
    {
        // given
        var request = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(request);

        // when & then
        // 같은 Username으로 다시 가입 시도
        await Assert.ThrowsAsync<InvalidOperationException>(async () => { await _authService.RegisterAsync(request); });
    }

    [Fact]
    public async Task LoginAsync_는_올바른_정보로_로그인한다()
    {
        // given
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        // when
        var loginRequest = new LoginRequest(registerRequest.UserName,
            registerRequest.Password);

        var response = await _authService.LoginAsync(loginRequest);
        
        // then
        Assert.NotNull(response);
        Assert.True(response.UserId > 0);
        Assert.Equal(registerRequest.UserName, response.UserName);
        Assert.Equal(registerRequest.Email, response.Email);
    }

    [Fact]
    public async Task LoginAsync_는_존재하지않는_User면_예외를_던진다()
    {
        // given
        var loginRequest = new LoginRequest("notexist", "password123");

        // when & then
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await _authService.LoginAsync(loginRequest);
        });
    }

    [Fact]
    public async Task LoginAsync_는_잘못된_비밀번호면_예외를_던진다()
    {
        // given
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);
        
        var loginRequest = new LoginRequest(registerRequest.UserName,
            "wrongpassword");
        
        // when & then
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _authService.LoginAsync(loginRequest);
        });
    }
}