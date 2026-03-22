using System.Collections.Concurrent;
using GameServer.Application.Common;
using GameServer.Application.Domains.Auth;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security;
using GameServer.Application.Security.Interface;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameServer.Tests.Application.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly Mock<IUserSessionRepository> _mockUserSessionRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly AuthService _authService;
    private readonly JwtOptions _jwtOptions;

    private readonly ConcurrentDictionary<long, User> _users = new();
    private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
    private long _idCounter = 0;

    public AuthServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUserSessionRepository = new Mock<IUserSessionRepository>();
        
        _passwordHasher = new PasswordHasher();

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

        SetupMocks();

        _authService = new AuthService(
            _mockUserRepository.Object,
            _passwordHasher,
            _mockUserSessionRepository.Object,
            _jwtTokenGenerator,
            jwtOptionsWrapper,
            NullLogger<AuthService>.Instance
        );
    }

    private void SetupMocks()
    {
        _mockUserRepository.Setup(r => r.AddAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string hash, string email, CancellationToken ct) =>
            {
                var user = User.Create(hash, email);
                var id = Interlocked.Increment(ref _idCounter);
                user.SetUserId(id);
                _users[id] = user;
                return user;
            });

        _mockUserRepository.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken ct) => _users.Values.FirstOrDefault(u => u.Email == email));

        _mockUserRepository.Setup(r => r.GetByIdAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long id, CancellationToken ct) => _users.TryGetValue(id, out var user) ? user : null);

        _mockUserRepository.Setup(r => r.IsEmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string email, CancellationToken ct) => _users.Values.Any(u => u.Email == email));

        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User user, CancellationToken ct) =>
            {
                if (!_users.ContainsKey(user.UserId)) return false;
                _users[user.UserId] = user;
                return true;
            });

        _mockUserRepository.Setup(r => r.UpdateRefreshTokenAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, string token, DateTime expiry, CancellationToken ct) =>
            {
                if (!_users.TryGetValue(userId, out var user)) return false;
                user.SetRefreshToken(token, expiry);
                return true;
            });

        _mockUserRepository.Setup(r => r.ClearRefreshTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, CancellationToken ct) =>
            {
                if (!_users.TryGetValue(userId, out var user)) return false;
                user.ClearRefreshToken();
                return true;
            });

        _mockUserSessionRepository.Setup(r => r.CreateSessionAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((long userId, string nick, string email, string pubId, CancellationToken ct) =>
            {
                var sessionId = Guid.NewGuid().ToString();
                var session = UserSession.Create(userId, email, nick, pubId, sessionId);
                _sessions[sessionId] = session;
                return session;
            });

        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken ct) => _sessions.TryGetValue(id, out var session) ? session : null);

        _mockUserSessionRepository.Setup(r => r.RemoveSessionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken ct) =>
            {
                _sessions.TryRemove(id, out _);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task 회원가입_시_새로운_사용자가_성공적으로_생성되고_저장소에_반영된다()
    {
        // given
        var password = "password123";
        var email = "test@example.com";

        // when
        var result = await _authService.RegisterAsync(password, email);

        // then
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        var user = result.Value;
        Assert.NotNull(user);
        Assert.True(user.UserId > 0);
        Assert.Equal(email, user.Email);

        // Repository에 저장되었는지 확인
        _mockUserRepository.Verify(r => r.AddAsync(It.IsAny<string>(), email, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 이미_가입된_이메일로_회원가입_시도_시_중복_에러로_실패한다()
    {
        // given
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);

        // when - 같은 email로 다시 가입 시도
        var result = await _authService.RegisterAsync(password, email);

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.EmailAlreadyTaken, result.Message);
    }

    [Fact]
    public async Task 정확한_이메일과_비밀번호로_로그인_시_액세스_토큰과_세션_정보를_반환한다()
    {
        // given - 회원가입
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);

        // when - 로그인
        var result = await _authService.LoginAsync(email, password, "test-device-id");

        // then
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);
        
        var loginResult = result.Value;
        Assert.NotNull(loginResult);
        Assert.NotNull(loginResult.User);
        Assert.NotNull(loginResult.Session);
        Assert.NotEmpty(loginResult.AccessToken);
        
        Assert.True(loginResult.User.UserId > 0);
        Assert.Equal(email, loginResult.User.Email);
        Assert.NotEmpty(loginResult.Session.SessionId);
        Assert.True(loginResult.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task 가입되지_않은_이메일로_로그인_시도_시_사용자를_찾을_수_없음_에러로_실패한다()
    {
        // given
        var email = "test@example.com";
        var password = "password123";

        // when
        var result = await _authService.LoginAsync(email, password, "test-device-id");

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.UserNotFound, result.Message);
    }

    [Fact]
    public async Task 로그인_시_비밀번호가_일치하지_않으면_인증_실패_에러를_반환한다()
    {
        // given - 회원가입
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);

        // when - 잘못된 비밀번호로 로그인 시도
        var result = await _authService.LoginAsync(email, "wrongpassword", "test-device-id");

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorMessages.InvalidCredentials, result.Message);
    }

    [Fact]
    public async Task 로그인_요청_시_필수_입력값이_누락되면_잘못된_요청_에러로_실패한다()
    {
        // given
        var email = "";
        var password = "password123";

        // when
        var result = await _authService.LoginAsync(email, password, "test-device-id");

        // then
        Assert.NotNull(result);
        Assert.False(result.IsSuccess);
        Assert.Contains(ErrorMessages.InvalidRequest, result.Message);
        Assert.Contains("email", result.Message);
    }

    [Fact]
    public async Task 로그아웃_요청_시_사용자의_세션과_리프레시_토큰_정보가_성공적으로_삭제된다()
    {
        // given - 회원가입 & 로그인
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);
        var loginResult = await _authService.LoginAsync(email, password, "test-device-id");
        var sessionId = loginResult.Value.Session.SessionId;

        // when - 로그아웃
        var result = await _authService.LogoutAsync(sessionId);

        // then - 로그아웃 성공
        Assert.NotNull(result);
        Assert.True(result.IsSuccess);

        // 세션이 삭제되었는지 확인
        _mockUserSessionRepository.Verify(r => r.RemoveSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(_sessions.ContainsKey(sessionId));
    }

    [Fact]
    public async Task 로그아웃_요청_시_세션_ID_값이_비어있으면_실패한다()
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
    public async Task 이미_로그아웃되었거나_존재하지_않는_세션_ID로_로그아웃_시도_시에도_성공으로_처리한다()
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
    public async Task 유효한_액세스_토큰을_검증하면_성공을_반환한다()
    {
        // given - 회원가입 & 로그인
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);
        var loginResult = await _authService.LoginAsync(email, password, "test-device-id");
        var accessToken = loginResult.Value.AccessToken;

        // when
        var isValid = await _authService.ValidateTokenAsync(accessToken);

        // then
        Assert.True(isValid);
    }

    [Fact]
    public async Task 토큰_값이_비어있는_경우_검증에_실패한다()
    {
        // given
        var token = "";

        // when
        var isValid = await _authService.ValidateTokenAsync(token);

        // then
        Assert.False(isValid);
    }

    [Fact]
    public async Task 위변조되거나_형식이_잘못된_토큰을_검증하면_실패한다()
    {
        // given
        var invalidToken = "invalid-token-string";

        // when
        var isValid = await _authService.ValidateTokenAsync(invalidToken);

        // then
        Assert.False(isValid);
    }

    [Fact]
    public async Task 토큰은_유효하지만_세션이_만료되거나_제거된_상태면_검증에_실패한다()
    {
        // given - 회원가입 & 로그인
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);
        var loginResult = await _authService.LoginAsync(email, password, "test-device-id");
        var accessToken = loginResult.Value.AccessToken;
        var sessionId = loginResult.Value.Session.SessionId;

        // 로그아웃 (세션 삭제)
        await _authService.LogoutAsync(sessionId);

        // when - 로그아웃 후 토큰 검증
        var isValid = await _authService.ValidateTokenAsync(accessToken);

        // then - JWT는 유효하지만 세션이 없으므로 실패
        Assert.False(isValid);
    }

    [Fact]
    public async Task 회원가입부터_로그인_토큰검증_로그아웃까지의_전체_인증_프로세스가_정상_작동한다()
    {
        // given
        var password = "password123";
        var email = "test@example.com";

        // 1. 회원가입
        var registerResult = await _authService.RegisterAsync(password, email);
        
        Assert.True(registerResult.IsSuccess);
        Assert.NotNull(registerResult.Value);
        Assert.Equal(email, registerResult.Value.Email);

        // 2. 로그인
        var loginResult = await _authService.LoginAsync(email, password, "test-device-id");
        
        Assert.True(loginResult.IsSuccess);
        Assert.NotNull(loginResult.Value);
        Assert.NotEmpty(loginResult.Value.AccessToken);
        Assert.NotEmpty(loginResult.Value.Session.SessionId);

        var accessToken = loginResult.Value.AccessToken;
        var sessionId = loginResult.Value.Session.SessionId;

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
    public async Task 동일한_계정으로_중복_로그인_시_매번_새로운_세션_ID가_발급됨을_확인한다()
    {
        // given - 회원가입
        var password = "password123";
        var email = "test@example.com";
        
        await _authService.RegisterAsync(password, email);

        // when - 첫 번째 로그인
        var firstLogin = await _authService.LoginAsync(email, password, "device-1");
        var firstSessionId = firstLogin.Value.Session.SessionId;

        // when - 두 번째 로그인 (같은 계정)
        var secondLogin = await _authService.LoginAsync(email, password, "device-2");
        var secondSessionId = secondLogin.Value.Session.SessionId;

        // then - 새로운 세션 ID가 생성됨
        Assert.NotEqual(firstSessionId, secondSessionId);

        // 첫 번째 세션은 여전히 존재 (현재 구현에서는 다중 세션 허용)
        Assert.True(_sessions.ContainsKey(firstSessionId));

        // 두 번째 세션도 존재
        Assert.True(_sessions.ContainsKey(secondSessionId));
    }

    [Fact]
    public async Task 로그인_성공_시_사용자_정보에_보안_해싱된_리프레시_토큰과_만료일이_저장된다()
    {
        // given
        var password = "password123";
        var email = "login_refresh@test.com";
        await _authService.RegisterAsync(password, email);

        // when
        var result = await _authService.LoginAsync(email, password, "test-device-id");

        // then
        Assert.True(result.IsSuccess);
        
        var user = _users.Values.First(u => u.Email == email);
        Assert.NotNull(user.RefreshToken);
        Assert.True(user.RefreshTokenExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task 만료되지_않은_리프레시_토큰을_사용하여_액세스_토큰_갱신_및_토큰_로테이션을_수행한다()
    {
        // given
        var password = "password123";
        var email = "refresh_success@test.com";
        await _authService.RegisterAsync(password, email);
        var loginResult = (await _authService.LoginAsync(email, password, "test-device-id")).Value;
        var oldAccessToken = loginResult.AccessToken;

        // when - 갱신 시도
        var result = await _authService.RefreshTokenAsync(oldAccessToken, loginResult.RefreshToken, "test-device-id");

        // then
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.AccessToken);
        Assert.NotEqual(oldAccessToken, result.Value.AccessToken);

        // DB 확인 (로테이션 확인)
        var user = _users.Values.First(u => u.Email == email);
        Assert.NotNull(user.RefreshToken);
    }

    [Fact]
    public async Task 서버에_저장된_리프레시_토큰이_만료된_상태에서_갱신_요청_시_실패한다()
    {
        // given
        var password = "password123";
        var email = "refresh_expired@test.com";
        await _authService.RegisterAsync(password, email);
        var loginResult = (await _authService.LoginAsync(email, password, "test-device-id")).Value;

        // 강제로 리프레시 토큰 만료시킴
        var user = _users.Values.First(u => u.Email == email);
        var expiredUser = User.FromRedis(user.UserId, user.Email, user.PublicId, user.PasswordHash, user.CreatedAt, user.NickName, user.RefreshToken, DateTime.UtcNow.AddDays(-1));
        _users[user.UserId] = expiredUser;

        // when
        var result = await _authService.RefreshTokenAsync(loginResult.AccessToken, loginResult.RefreshToken, "test-device-id");

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionExpired, result.InternalErrorCode);
    }

    [Fact]
    public async Task 액세스_토큰의_수명이_다했어도_리프레시_토큰이_유효하면_성공적으로_새_토큰을_발급한다()
    {
        // given
        var password = "password123";
        var email = "refresh_expired_access@test.com";
        await _authService.RegisterAsync(password, email);
        
        // 액세스 토큰 만료 시간을 아주 짧게 설정한 옵션으로 새로 생성
        var shortJwtOptions = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            Secret = "test-secret-key-at-least-32-chars-long",
            AccessTokenMinutes = -1 // 이미 만료됨
        };
        var generator = new JwtTokenGenerator(Options.Create(shortJwtOptions));
        
        var loginResult = (await _authService.LoginAsync(email, password, "test-device-id")).Value;
        var expiredAccessToken = generator.GenerateAccessToken(
            loginResult.User.UserId, loginResult.User.NickName, loginResult.User.Email, loginResult.Session.SessionId);

        // when
        // AuthService 내부에서 validateLifetime: false를 사용하므로 만료되었어도 통과해야 함
        var result = await _authService.RefreshTokenAsync(expiredAccessToken, loginResult.RefreshToken, "test-device-id");

        // then
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.AccessToken);
        Assert.NotEqual(expiredAccessToken, result.Value.AccessToken);
    }

    [Fact]
    public async Task 서버에_리프레시_토큰이_없는_상태에서_갱신_요청_시_보안을_위해_세션을_강제_종료하고_실패한다()
    {
        // given
        var password = "password123";
        var email = "refresh_null@test.com";
        await _authService.RegisterAsync(password, email);
        var loginResult = (await _authService.LoginAsync(email, password, "test-device-id")).Value;
        var sessionId = loginResult.Session.SessionId;

        // DB에서 RefreshToken을 강제로 null로 만듦
        var user = _users.Values.First(u => u.Email == email);
        user.ClearRefreshToken();

        // when
        var result = await _authService.RefreshTokenAsync(loginResult.AccessToken, "dummy-token", "test-device-id");

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);

        // 세션이 삭제되었는지 확인 (강제 종료 확인)
        Assert.False(_sessions.ContainsKey(sessionId));
    }

    [Fact]
    public async Task 다른_디바이스_ID로_리프레시_시도_시_바인딩_실패로_거부된다()
    {
        // given
        var password = "password123";
        var email = "refresh_binding_fail@test.com";
        await _authService.RegisterAsync(password, email);
        
        // device-A로 로그인
        var loginResult = (await _authService.LoginAsync(email, password, "device-A")).Value;
        
        // when - device-B로 리프레시 시도
        var result = await _authService.RefreshTokenAsync(loginResult.AccessToken, loginResult.RefreshToken, "device-B");
        
        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionExpired, result.InternalErrorCode);
    }

    [Fact]
    public async Task 잘못된_리프레시_토큰으로_리프레시_시도_시_거부된다()
    {
        // given
        var password = "password123";
        var email = "refresh_token_fail@test.com";
        await _authService.RegisterAsync(password, email);
        
        var loginResult = (await _authService.LoginAsync(email, password, "device-A")).Value;
        
        // when - 잘못된 리프레시 토큰으로 시도
        var result = await _authService.RefreshTokenAsync(loginResult.AccessToken, "invalid-refresh-token", "device-A");
        
        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.SessionExpired, result.InternalErrorCode);
    }
}
