using GameServer.Application.Common;
using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.User;
using Moq;

namespace GameServer.Tests.Application.Services;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUserSessionRepository> _mockUserSessionRepository;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUserSessionRepository = new Mock<IUserSessionRepository>();
        _userService = new UserService(_mockUserRepository.Object, _mockUserSessionRepository.Object);
    }

    private (string sessionId, User user, UserSession session) CreateTestData(long userId = 1, string email = "test@example.com")
    {
        var user = User.Create("hashed_password", email);
        user.SetUserId(userId);
        var sessionId = Guid.NewGuid().ToString();
        var session = UserSession.Create(userId, email, user.NickName, user.PublicId, sessionId);
        return (sessionId, user, session);
    }

    [Fact]
    public async Task 유효한_세션으로_사용자_프로필_조회_시_정확한_사용자_정보를_반환한다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // when
        var result = await _userService.GetProfileAsync(sessionId);

        // then
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(user.UserId, result.Value.UserId);
        Assert.Equal(user.Email, result.Value.Email);
    }

    [Fact]
    public async Task 로그아웃되었거나_존재하지_않는_세션으로_프로필_조회_시_실패한다()
    {
        // given
        var invalidSessionId = "invalid-session";
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(invalidSessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        // when
        var result = await _userService.GetProfileAsync(invalidSessionId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 유효하지_않은_세션으로_닉네임_변경_시도_시_실패한다()
    {
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var result = await _userService.SetNicknameAsync("invalid-session", "NewNick");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }
    
    [Fact]
    public async Task 새로운_닉네임으로_변경_요청_시_사용자_정보와_저장소에_정상적으로_반영된다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var newNickname = "NewNickname";
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.IsNicknameExistsAsync(newNickname, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // when
        var result = await _userService.SetNicknameAsync(sessionId, newNickname);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newNickname, result.Value!.NickName);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => u.NickName == newNickname), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 이미_다른_사용자가_사용_중인_닉네임으로_변경하려고_하면_실패한다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var nickname = "DuplicateNick";
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.IsNicknameExistsAsync(nickname, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // when
        var result = await _userService.SetNicknameAsync(sessionId, nickname);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NickNameAlreadyTaken, result.InternalErrorCode);
    }

    [Fact]
    public async Task 길이가_너무_짧거나_허용되지_않는_문자가_포함된_닉네임으로_변경_시도_시_실패한다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var invalidNickname = "a"; // 너무 짧음 (최소 3자)
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // when
        var result = await _userService.SetNicknameAsync(sessionId, invalidNickname);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task 새로운_이메일_주소로_변경_요청_시_성공적으로_업데이트된다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var newEmail = "newemail@example.com";
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.IsEmailExistsAsync(newEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // when
        var result = await _userService.SetEmailAsync(sessionId, newEmail);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newEmail, result.Value!.Email);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Email == newEmail), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 이미_가입된_다른_사용자의_이메일로_변경하려고_하면_실패한다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var email = "taken@example.com";
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.IsEmailExistsAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // when
        var result = await _userService.SetEmailAsync(sessionId, email);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.EmailAlreadyTaken, result.InternalErrorCode);
    }

    [Fact]
    public async Task 닉네임과_이메일을_동시에_수정_요청_시_두_정보가_모두_성공적으로_반영된다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        var newNickname = "UpdatedNick";
        var newEmail = "updated@example.com";
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockUserRepository.Setup(r => r.IsNicknameExistsAsync(newNickname, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUserRepository.Setup(r => r.IsEmailExistsAsync(newEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockUserRepository.Setup(r => r.GetByIdAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _mockUserRepository.Setup(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // when
        var result = await _userService.UpdateProfileAsync(sessionId, newNickname, newEmail);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newNickname, result.Value!.NickName);
        Assert.Equal(newEmail, result.Value!.Email);
        _mockUserRepository.Verify(r => r.UpdateAsync(It.Is<User>(u => u.NickName == newNickname && u.Email == newEmail), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task 프로필_업데이트_시_필수_값이_누락되면_실패한다()
    {
        // given
        var (sessionId, user, session) = CreateTestData();
        
        _mockUserSessionRepository.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        // when
        var result = await _userService.UpdateProfileAsync(sessionId, "", "test@test.com");

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }
}
