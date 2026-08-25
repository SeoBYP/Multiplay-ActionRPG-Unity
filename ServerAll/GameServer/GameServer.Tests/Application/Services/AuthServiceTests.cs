using GameServer.Application.Common;
using GameServer.Application.Domains.Account;
using GameServer.Application.Domains.Auth;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using GameServer.Infrastructure.Security;
using GameServer.Tests.Infrastructure.Fakes.Repositories;
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
            _profileRepository,
            _credentialRepository,
            _passwordHasher,
            NullLogger<AccountService>.Instance);
        _authService = new AuthService(
            _accountService,
            _userRepository,
            _credentialRepository,
            _sessionRepository,
            _profileRepository,
            new GameServer.Tests.Infrastructure.Fakes.Services.FakeUserPositionService(),
            _jwtTokenGenerator,
            jwtOptionsWrapper,
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task CorrectEmailAndPassword_로그인_성공_토큰과_세션_반환()
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
    public async Task MissingUser_사용자_없음_로그인_실패()
    {
        var result = await _authService.LoginAsync("missing@example.com", "Password123!", "device-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.InternalErrorCode);
    }

    [Fact]
    public async Task WrongPassword_비밀번호_불일치_로그인_실패()
    {
        await RegisterWithProfileAsync("test@example.com", "Password123!", "tester");

        var result = await _authService.LoginAsync("test@example.com", "WrongPassword!", "device-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.InternalErrorCode);
    }

    [Fact]
    public async Task SuccessfulLogin_리프레시_토큰_저장_확인()
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
    public async Task Logout_세션_및_리프레시_토큰_삭제()
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
    public async Task ValidToken_유효한_토큰_검증_성공()
    {
        await RegisterWithProfileAsync("validate@example.com", "Password123!", "validate_user");
        var login = await _authService.LoginAsync("validate@example.com", "Password123!", "device-1");

        var isValid = await _authService.ValidateTokenAsync(login.Value!.AccessToken);

        Assert.True(isValid);
    }

    [Fact]
    public async Task LoggedOutToken_로그아웃된_토큰_검증_실패()
    {
        await RegisterWithProfileAsync("validate-logout@example.com", "Password123!", "validate_logout_user");
        var login = await _authService.LoginAsync("validate-logout@example.com", "Password123!", "device-1");
        await _authService.LogoutAsync(login.Value!.Session.SessionId);

        var isValid = await _authService.ValidateTokenAsync(login.Value.AccessToken);

        Assert.False(isValid);
    }

    [Fact]
    public async Task RefreshToken_토큰_갱신_성공()
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
    public async Task 새_기기_로그인_시_이전_세션은_강제_만료된다()
    {
        await RegisterWithProfileAsync("single@example.com", "Password123!", "single_user");

        // 기기 A 로그인 → 같은 계정으로 기기 B 로그인(단일 세션 정책)
        var loginA = await _authService.LoginAsync("single@example.com", "Password123!", "device-A");
        var loginB = await _authService.LoginAsync("single@example.com", "Password123!", "device-B");

        // 기기 A의 옛 세션은 제거되어 ValidateToken(세션 저장소 검증)이 실패 = 강제 만료. 기기 B만 유효.
        Assert.False(await _authService.ValidateTokenAsync(loginA.Value!.AccessToken));
        Assert.True(await _authService.ValidateTokenAsync(loginB.Value!.AccessToken));
        Assert.Null(await _sessionRepository.GetBySessionIdAsync(loginA.Value.Session.SessionId));
    }

    [Fact]
    public async Task RefreshToken_다른_기기가_제출하면_실패하지만_세션은_유지된다()
    {
        await RegisterWithProfileAsync("binding@example.com", "Password123!", "binding_user");
        var login = await _authService.LoginAsync("binding@example.com", "Password123!", "device-A");

        var result = await _authService.RefreshTokenAsync(login.Value!.AccessToken, login.Value.RefreshToken, "device-B");

        Assert.False(result.IsSuccess);
        // 소유 증명을 통과하지 못한 요청은 실패만 시킨다. 세션·리프레시 토큰을 파괴하면 DoS 벡터가 된다.
        Assert.NotNull(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));

        // 정상 기기는 그대로 갱신할 수 있어야 한다.
        var retry = await _authService.RefreshTokenAsync(login.Value.AccessToken, login.Value.RefreshToken, "device-A");
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public async Task RefreshToken_위조된_리프레시_문자열로는_세션을_파괴할_수_없다()
    {
        var register = await RegisterWithProfileAsync("forge@example.com", "Password123!", "forge_user");
        var login = await _authService.LoginAsync("forge@example.com", "Password123!", "device-A");

        // 유출된 (만료된) accessToken 하나만 있으면 되는 공격: 형식만 맞춘 아무 문자열 + 버전 역행
        var forged = await _authService.RefreshTokenAsync(login.Value!.AccessToken, "aaa.0", "attacker-device");

        Assert.False(forged.IsSuccess);
        Assert.NotEqual(ErrorCodes.TokenReuseDetected, forged.InternalErrorCode);
        Assert.NotNull(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));

        var credential = await _credentialRepository.FindByIdAsync(register.UserId);
        Assert.NotNull(credential!.RefreshToken);

        var retry = await _authService.RefreshTokenAsync(login.Value.AccessToken, login.Value.RefreshToken, "device-A");
        Assert.True(retry.IsSuccess);
    }

    [Fact]
    public async Task RefreshToken_저장된_리프레시_토큰이_없어도_세션을_파괴하지_않는다()
    {
        var register = await RegisterWithProfileAsync("notoken@example.com", "Password123!", "notoken_user");
        var login = await _authService.LoginAsync("notoken@example.com", "Password123!", "device-A");
        await _credentialRepository.ClearRefreshTokenAsync(register.UserId);

        var result = await _authService.RefreshTokenAsync(login.Value!.AccessToken, login.Value.RefreshToken, "device-A");

        Assert.False(result.IsSuccess);
        Assert.NotNull(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));
    }

    [Fact]
    public async Task ExpiredAccessToken_만료된_액세스_토큰으로도_갱신_허용()
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
            login.Value.User.PublicId,
            "expired-access@example.com",
            login.Value.Session.SessionId);

        var result = await _authService.RefreshTokenAsync(expiredAccessToken, login.Value.RefreshToken, "device-1");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.AccessToken);
    }

    [Fact]
    public async Task ReuseRefreshToken_유예_경과_후_구버전_토큰_재사용은_세션을_종료한다()
    {
        // 유예 0초 = 회전 직후의 재시도 관용을 끈 상태. 구버전 토큰 제출은 곧바로 탈취로 본다.
        var authService = CreateAuthService(refreshReuseGraceSeconds: 0);

        // 1. 로그인 -> refreshToken_v1 발급
        var register = await RegisterWithProfileAsync("reuse@example.com", "Password123!", "reuse_user");
        var login = await authService.LoginAsync("reuse@example.com", "Password123!", "device-1");
        var refreshTokenV1 = login.Value!.RefreshToken;
        var sessionId = login.Value.Session.SessionId;

        // 2. RefreshToken(refreshToken_v1) -> 성공, refreshToken_v2 발급
        var refreshResult = await authService.RefreshTokenAsync(login.Value.AccessToken, refreshTokenV1, "device-1");
        Assert.True(refreshResult.IsSuccess);
        var refreshTokenV2 = refreshResult.Value!.RefreshToken;
        Assert.NotEqual(refreshTokenV1, refreshTokenV2);

        // 3. RefreshToken(refreshToken_v1) 재시도 -> TokenReuseDetected 반환 및 세션 제거
        var reuseResult = await authService.RefreshTokenAsync(refreshResult.Value.AccessToken, refreshTokenV1, "device-1");

        Assert.False(reuseResult.IsSuccess);
        Assert.Equal(ErrorCodes.TokenReuseDetected, reuseResult.InternalErrorCode);

        // 세션이 제거되었는지 확인
        Assert.Null(await _sessionRepository.GetBySessionIdAsync(sessionId));
        
        // 리프레시 토큰이 무효화되었는지 확인
        var credential = await _credentialRepository.FindByIdAsync(register.UserId);
        Assert.Null(credential!.RefreshToken);
    }

    [Fact]
    public async Task ReuseRefreshToken_유예_시간_내_중복_요청은_재시도로_처리된다()
    {
        // 응답 유실 후 클라이언트가 같은 토큰으로 다시 보내는 상황. 탈취로 오판하면 대량 로그아웃이 된다.
        await RegisterWithProfileAsync("retry@example.com", "Password123!", "retry_user");
        var login = await _authService.LoginAsync("retry@example.com", "Password123!", "device-1");
        var refreshTokenV1 = login.Value!.RefreshToken;

        var first = await _authService.RefreshTokenAsync(login.Value.AccessToken, refreshTokenV1, "device-1");
        Assert.True(first.IsSuccess);

        var retry = await _authService.RefreshTokenAsync(first.Value!.AccessToken, refreshTokenV1, "device-1");

        Assert.True(retry.IsSuccess);
        Assert.NotEqual(refreshTokenV1, retry.Value!.RefreshToken);
        Assert.NotNull(await _sessionRepository.GetBySessionIdAsync(login.Value.Session.SessionId));
    }

    private AuthService CreateAuthService(int refreshReuseGraceSeconds)
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Secret = _jwtOptions.Secret,
            AccessTokenMinutes = _jwtOptions.AccessTokenMinutes,
            RefreshTokenExpirationHours = _jwtOptions.RefreshTokenExpirationHours,
            RefreshReuseGraceSeconds = refreshReuseGraceSeconds
        });

        return new AuthService(
            _accountService,
            _userRepository,
            _credentialRepository,
            _sessionRepository,
            _profileRepository,
            new GameServer.Tests.Infrastructure.Fakes.Services.FakeUserPositionService(),
            new JwtTokenGenerator(options),
            options,
            NullLogger<AuthService>.Instance);
    }

    private async Task<GameServer.Domain.Entities.User.User> RegisterWithProfileAsync(string email, string password, string nickName)
    {
        var register = await _accountService.RegisterAsync(email, password);
        Assert.True(register.IsSuccess);
        var user = register.Value!;
        var profile = await _profileRepository.GetByIdAsync(user.UserId);
        if (profile is not null && profile.NickName != nickName)
        {
            profile.SetNickName(nickName);
            await _profileRepository.UpdateAsync(profile);
        }
        return user;
    }
}
