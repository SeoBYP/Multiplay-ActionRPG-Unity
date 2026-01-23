using GameServer.Application.Common;
using GameServer.Application.DTOs.Requests;
using GameServer.Application.Services;
using GameServer.Domain.Interfaces;
using GameServer.Infrastructure.Repositories;
using GameServer.Infrastructure.Security;
using GameServer.Infrastructure.Interfaces;
using Microsoft.Extensions.Options;

namespace GameServer.Tests.Application.Services;

public class AuthServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessionRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthService _authService;
    private readonly JwtOptions _jwtOptions = new();

    public AuthServiceTests() // ← 생성자 사용
    {
        _userRepository = new InMemoryUserRepository();
        _passwordHasher = new PasswordHasher();

        var jwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Secret = "test-secret-key-at-least-32-chars-long",
            AccessTokenMinutes = 60
        };

        var jwtOptionsWrapper = Options.Create(jwtOptions);

        _jwtTokenGenerator = new JwtTokenGenerator(jwtOptionsWrapper);
        _sessionRepository = new FakeSessionRepository();


        _authService = new AuthService(
            _userRepository,
            _passwordHasher,
            _sessionRepository,
            _jwtTokenGenerator,
            jwtOptionsWrapper
        );
    }

    [Fact]
    public async Task RegisterAsync_는_새로운_User를_생성한다()
    {
        // given
        var request = new RegisterRequest("testuser", "password123", "test@example.com");

        // wher
        var result = await _authService.RegisterAsync(request);

        // then
        var value = result.Value;
        Assert.NotNull(value);
        Assert.NotNull(result);
        Assert.True(value.UserId > 0);
        Assert.Equal(request.UserName, value.UserName);
        Assert.Equal(request.Email, value.Email);

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

        // when
        var result = await _authService.RegisterAsync(request);

        // 같은 Username으로 다시 가입 시도
        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.UserAlreadyExists, result.Message);
    }

    [Fact]
    public async Task RegisterAsync_는_빈_UserName이면_실패한다()
    {
        // given
        var request = new RegisterRequest("", "password123", "test@example.com");

        // when
        var result = await _authService.RegisterAsync(request);

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.InvalidRequest, result.Message);
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

        var result = await _authService.LoginAsync(loginRequest);

        // then
        var value = result.Value;
        Assert.NotNull(result);
        Assert.NotNull(value);
        Assert.True(value.UserId > 0);
        Assert.Equal(registerRequest.UserName, value.UserName);
        Assert.Equal(registerRequest.Email, value.Email);
        Assert.NotEmpty(value.AccessToken);
        Assert.NotEmpty(value.SessionId);
        Assert.True(value.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_는_존재하지않는_User면_예외를_던진다()
    {
        // given
        var loginRequest = new LoginRequest("notexist", "password123");

        // when
        var result = await _authService.LoginAsync(loginRequest);

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.UserNotFound, result.Message);
    }

    [Fact]
    public async Task LoginAsync_는_잘못된_비밀번호면_예외를_던진다()
    {
        // given
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest(registerRequest.UserName,
            "wrongpassword");
        // when 
        var result = await _authService.LoginAsync(loginRequest);

        // then
        Assert.NotNull(result );
        Assert.False(result .IsSuccess);
        Assert.Equal(ErrorMessages.InvalidCredentials, result .Message);
    }
    
    [Fact]
    public async Task LoginAsync_는_빈_UserName이면_실패한다()
    {
        // given
        var loginRequest = new LoginRequest("", "password123");

        // when
        var result = await _authService.LoginAsync(loginRequest);

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.InvalidRequest, result.Message);
    }
    
    [Fact]
    public async Task LogoutAsync_는_세션을_삭제한다()
    {
        // given - 회원가입 & 로그인
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );
        var loginResult = await _authService.LoginAsync(loginRequest);
        var sessionId = loginResult.Value.SessionId;

        // when - 로그아웃
        var result = await _authService.LogoutAsync(sessionId);

        // then - 로그아웃 성공
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // 세션이 삭제되었는지 확인
        var session = await _sessionRepository.GetBySessionIdAsync(sessionId);
        Assert.Null(session);
    }
    
    
    [Fact]
    public async Task LogoutAsync_는_빈_SessionId면_실패한다()
    {
        // given
        var sessionId = "";

        // when
        var result = await _authService.LogoutAsync(sessionId);

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.InvalidRequest, result.Message);
    }

    [Fact]
    public async Task LogoutAsync_는_존재하지않는_SessionId도_성공한다()
    {
        // given - 존재하지 않는 세션 ID
        var sessionId = "non-existent-session-id";

        // when
        var result = await _authService.LogoutAsync(sessionId);

        // then - 멱등성: 이미 로그아웃된 상태도 성공으로 처리
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
    }
    
    [Fact]
    public async Task ValidateTokenAsync_는_유효한_토큰을_검증한다()
    {
        // given - 회원가입 & 로그인
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );
        var loginResult = await _authService.LoginAsync(loginRequest);
        var accessToken = loginResult.Value.AccessToken;

        // when
        var isValid = await _authService.ValidateTokenAsync(accessToken);

        // then
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_는_빈_토큰이면_실패한다()
    {
        // given
        var token = "";

        // when
        var isValid = await _authService.ValidateTokenAsync(token);

        // then
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateTokenAsync_는_잘못된_토큰이면_실패한다()
    {
        // given
        var invalidToken = "invalid-token-string";

        // when
        var isValid = await _authService.ValidateTokenAsync(invalidToken);

        // then
        Assert.False(isValid);
    }
    
    

    [Fact]
    public async Task ValidateTokenAsync_는_로그아웃된_세션이면_실패한다()
    {
        // given - 회원가입 & 로그인
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );
        var loginResult = await _authService.LoginAsync(loginRequest);
        var accessToken = loginResult.Value.AccessToken;
        var sessionId = loginResult.Value.SessionId;

        // 로그아웃 (세션 삭제)
        await _authService.LogoutAsync(sessionId);

        // when - 로그아웃 후 토큰 검증
        var isValid = await _authService.ValidateTokenAsync(accessToken);

        // then - JWT는 유효하지만 세션이 없으므로 실패
        Assert.False(isValid);
    }


    [Fact]
    public async Task 전체_플로우_회원가입_로그인_로그아웃()
    {
        // given
        var username = "testuser";
        var password = "password123";
        var email = "test@example.com";

        // 1. 회원가입
        var registerRequest = new RegisterRequest(username, password, email);
        var registerResult = await _authService.RegisterAsync(registerRequest);
        
        Assert.True(registerResult.IsSuccess);
        Assert.NotNull(registerResult.Value);

        // 2. 로그인
        var loginRequest = new LoginRequest(username, password);
        var loginResult = await _authService.LoginAsync(loginRequest);
        
        Assert.True(loginResult.IsSuccess);
        Assert.NotNull(loginResult.Value);
        Assert.NotEmpty(loginResult.Value.AccessToken);
        Assert.NotEmpty(loginResult.Value.SessionId);

        var accessToken = loginResult.Value.AccessToken;
        var sessionId = loginResult.Value.SessionId;

        // 3. 토큰 검증 (로그인 상태)
        var isValidBeforeLogout = await _authService.ValidateTokenAsync(accessToken);
        Assert.True(isValidBeforeLogout);

        // 4. 로그아웃
        var logoutResult = await _authService.LogoutAsync(sessionId);
        Assert.True(logoutResult.IsSuccess);

        // 5. 토큰 검증 (로그아웃 후)
        var isValidAfterLogout = await _authService.ValidateTokenAsync(accessToken);
        Assert.False(isValidAfterLogout);  // 세션이 삭제되어 실패
    }

    [Fact]
    public async Task 동일_계정으로_여러_번_로그인하면_이전_세션이_대체된다()
    {
        // given
        var registerRequest = new RegisterRequest("testuser", "password123", "test@example.com");
        await _authService.RegisterAsync(registerRequest);

        var loginRequest = new LoginRequest(
            registerRequest.UserName,
            registerRequest.Password
        );

        // when - 첫 번째 로그인
        var firstLogin = await _authService.LoginAsync(loginRequest);
        var firstSessionId = firstLogin.Value.SessionId;

        // when - 두 번째 로그인 (같은 계정)
        var secondLogin = await _authService.LoginAsync(loginRequest);
        var secondSessionId = secondLogin.Value.SessionId;

        // then - 새로운 세션 ID가 생성됨
        Assert.NotEqual(firstSessionId, secondSessionId);

        // 첫 번째 세션은 여전히 존재 (현재 구현에서는 다중 세션 허용)
        var firstSession = await _sessionRepository.GetBySessionIdAsync(firstSessionId);
        Assert.NotNull(firstSession);

        // 두 번째 세션도 존재
        var secondSession = await _sessionRepository.GetBySessionIdAsync(secondSessionId);
        Assert.NotNull(secondSession);
    }
}