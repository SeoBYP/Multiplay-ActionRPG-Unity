using GameServer.Application.Common;
using GameServer.Application.Domains.Account;
using GameServer.Application.Domains.Auth;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using GameServer.Infrastructure.Security;
using GameServer.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameServer.Tests.Application.Services;

public class AuthServiceTests
{
    private readonly FakeUserRepository _userRepository = new();
    private readonly FakeUserCredentialRepository _credentialRepository = new();
    private readonly FakeUserSessionRepository _sessionRepository = new();
    private readonly FakeUserProfileRepository _profileRepository = new();
    private readonly IPasswordHasher _passwordHasher = new PasswordHasher();
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtOptions _jwtOptions;
    private readonly AccountService _accountService;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Secret = "test-secret-key-at-least-32-chars-long",
            AccessTokenMinutes = 60,
            RefreshTokenExpirationHours = 24
        };

        var jwtOptionsWrapper = Options.Create(_jwtOptions);
        _jwtTokenGenerator = new JwtTokenGenerator(jwtOptionsWrapper);
        _accountService = new AccountService(
            _userRepository,
            _credentialRepository,
            _passwordHasher,
            NullLogger<AccountService>.Instance);
        _authService = new AuthService(
            _accountService,
            _userRepository,
            _credentialRepository,
            _sessionRepository,
            _profileRepository,
            _jwtTokenGenerator,
            jwtOptionsWrapper,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task CorrectEmailAndPassword_ReturnsTokensAndSession()
    {
        await RegisterWithProfileAsync("test@example.com", "Password123!", "tester");

        var result = await _authService.LoginAsync("test@example.com", "Password123!", "device-1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEmpty(result.Value!.AccessToken);
        Assert.NotEmpty(result.Value.RefreshToken);
        Assert.NotEmpty(result.Value.Session.SessionId);
        Assert.True(result.Value.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task MissingUser_FailsLogin()
    {
        var result = await _authService.LoginAsync("missing@example.com", "Password123!", "device-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task WrongPassword_FailsLogin()
    {
        await RegisterWithProfileAsync("test@example.com", "Password123!", "tester");

        var result = await _authService.LoginAsync("test@example.com", "WrongPassword!", "device-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.InternalErrorCode);
    }

    [Fact]
    public async Task SuccessfulLogin_PersistsRefreshToken()
    {
        var register = await RegisterWithProfileAsync("refresh@example.com", "Password123!", "refresh_user");

        var result = await _authService.LoginAsync("refresh@example.com", "Password123!", "device-1");

        Assert.True(result.IsSuccess);
        var credential = await _credentialRepository.FindByIdAsync(register.UserId);
        Assert.NotNull(credential);
        Assert.NotNull(credential!.RefreshToken);
        Assert.True(credential.RefreshTokenExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Logout_ClearsSessionAndRefreshToken()
    {
        var register = await RegisterWithProfileAsync("logout@example.com", "Password123!", "logout_user");
        var login = await _authService.LoginAsync("logout@example.com", "Password123!", "device-1");

        var result = await _authService.LogoutAsync(login.Value!.Session.SessionId);

        Assert.True(result.IsSuccess);
        Assert.Null(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));
        var credential = await _credentialRepository.FindByIdAsync(register.UserId);
        Assert.Null(credential!.RefreshToken);
    }

    [Fact]
    public async Task ValidToken_ReturnsTrue()
    {
        await RegisterWithProfileAsync("validate@example.com", "Password123!", "validate_user");
        var login = await _authService.LoginAsync("validate@example.com", "Password123!", "device-1");

        var isValid = await _authService.ValidateTokenAsync(login.Value!.AccessToken);

        Assert.True(isValid);
    }

    [Fact]
    public async Task LoggedOutToken_ReturnsFalse()
    {
        await RegisterWithProfileAsync("validate-logout@example.com", "Password123!", "validate_logout_user");
        var login = await _authService.LoginAsync("validate-logout@example.com", "Password123!", "device-1");
        await _authService.LogoutAsync(login.Value!.Session.SessionId);

        var isValid = await _authService.ValidateTokenAsync(login.Value.AccessToken);

        Assert.False(isValid);
    }

    [Fact]
    public async Task RefreshToken_RotatesTokens()
    {
        var register = await RegisterWithProfileAsync("rotate@example.com", "Password123!", "rotate_user");
        var login = await _authService.LoginAsync("rotate@example.com", "Password123!", "device-1");

        var result = await _authService.RefreshTokenAsync(login.Value!.AccessToken, login.Value.RefreshToken, "device-1");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(login.Value.AccessToken, result.Value!.AccessToken);
        Assert.NotEqual(login.Value.RefreshToken, result.Value.RefreshToken);

        var credential = await _credentialRepository.FindByIdAsync(register.UserId);
        Assert.NotNull(credential!.RefreshToken);
    }

    [Fact]
    public async Task RefreshToken_WithDifferentDevice_ExpiresSession()
    {
        await RegisterWithProfileAsync("binding@example.com", "Password123!", "binding_user");
        var login = await _authService.LoginAsync("binding@example.com", "Password123!", "device-A");

        var result = await _authService.RefreshTokenAsync(login.Value!.AccessToken, login.Value.RefreshToken, "device-B");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionExpired, result.InternalErrorCode);
        Assert.Null(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));
    }

    [Fact]
    public async Task ExpiredAccessToken_StillAllowsRefresh()
    {
        await RegisterWithProfileAsync("expired-access@example.com", "Password123!", "expired_user");
        var login = await _authService.LoginAsync("expired-access@example.com", "Password123!", "device-1");

        var expiredGenerator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Secret = _jwtOptions.Secret,
            AccessTokenMinutes = -1,
            RefreshTokenExpirationHours = _jwtOptions.RefreshTokenExpirationHours
        }));

        var expiredAccessToken = expiredGenerator.GenerateAccessToken(
            login.Value!.User.UserId,
            "expired_user",
            "expired-access@example.com",
            login.Value.Session.SessionId);

        var result = await _authService.RefreshTokenAsync(expiredAccessToken, login.Value.RefreshToken, "device-1");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.AccessToken);
    }

    private async Task<GameServer.Domain.Entities.User.User> RegisterWithProfileAsync(string email, string password, string nickName)
    {
        var register = await _accountService.RegisterAsync(email, password);
        Assert.True(register.IsSuccess);
        var user = register.Value!;
        await _profileRepository.CreateAsync(user.UserId, nickName);
        return user;
    }
}
