using GameServer.Application.Common;
using GameServer.Application.Services.User;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Tests.Infrastructure;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Application.Services;

public class UserServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userRepository = new FakeUserRepository();
        _userSessionRepository = new FakeUserSessionRepository();
        _userService = new UserService(_userRepository, _userSessionRepository);
    }

    private async Task<(string sessionId, User user)> CreateTestUserAndSessionAsync(string email = "test@example.com")
    {
        var user = await _userRepository.AddAsync("hashed_password", email);
        var session = await _userSessionRepository.CreateSessionAsync(user.UserId, user.NickName, user.Email, user.PublicId);
        return (session!.SessionId, user);
    }

    [Fact]
    public async Task GetProfileAsync_는_유효한_세션이면_프로필을_반환한다()
    {
        // given
        var (sessionId, user) = await CreateTestUserAndSessionAsync();

        // when
        var result = await _userService.GetProfileAsync(sessionId);

        // then
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(user.UserId, result.Value.UserId);
        Assert.Equal(user.Email, result.Value.Email);
    }

    [Fact]
    public async Task GetProfileAsync_는_유효하지_않은_세션이면_실패한다()
    {
        // given
        var invalidSessionId = "invalid-session";

        // when
        var result = await _userService.GetProfileAsync(invalidSessionId);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task SetNicknameAsync_는_존해하지_않는_세션이면_실패한다()
    {
        var result = await _userService.SetNicknameAsync("invalid-session", "NewNick");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }
    
    [Fact]
    public async Task SetNicknameAsync_는_닉네임을_변경한다()
    {
        // given
        var (sessionId, user) = await CreateTestUserAndSessionAsync();
        var newNickname = "NewNickname";

        // when
        var result = await _userService.SetNicknameAsync(sessionId, newNickname);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newNickname, result.Value!.NickName);

        var updatedUser = await _userRepository.GetByIdAsync(user.UserId);
        Assert.Equal(newNickname, updatedUser!.NickName);
    }

    [Fact]
    public async Task SetNicknameAsync_는_중복된_닉네임이면_실패한다()
    {
        // given
        var (sessionId1, user1) = await CreateTestUserAndSessionAsync("user1@example.com");
        var (sessionId2, user2) = await CreateTestUserAndSessionAsync("user2@example.com");
        
        var nickname = "DuplicateNick";
        await _userService.SetNicknameAsync(sessionId1, nickname);

        // when
        var result = await _userService.SetNicknameAsync(sessionId2, nickname);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NickNameAlreadyTaken, result.InternalErrorCode);
    }

    [Fact]
    public async Task SetNicknameAsync_는_유효하지_않은_닉네임_형식이면_실패한다()
    {
        // given
        var (sessionId, _) = await CreateTestUserAndSessionAsync();
        var invalidNickname = "a"; // 너무 짧음 (최소 3자)

        // when
        var result = await _userService.SetNicknameAsync(sessionId, invalidNickname);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }

    [Fact]
    public async Task SetEmailAsync_는_이메일을_변경한다()
    {
        // given
        var (sessionId, user) = await CreateTestUserAndSessionAsync();
        var newEmail = "newemail@example.com";

        // when
        var result = await _userService.SetEmailAsync(sessionId, newEmail);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newEmail, result.Value!.Email);

        var updatedUser = await _userRepository.GetByIdAsync(user.UserId);
        Assert.Equal(newEmail, updatedUser!.Email);
    }

    [Fact]
    public async Task SetEmailAsync_는_중복된_이메일이면_실패한다()
    {
        // given
        var (sessionId1, user1) = await CreateTestUserAndSessionAsync("user1@example.com");
        var (sessionId2, user2) = await CreateTestUserAndSessionAsync("user2@example.com");
        
        var email = "taken@example.com";
        await _userService.SetEmailAsync(sessionId1, email);

        // when
        var result = await _userService.SetEmailAsync(sessionId2, email);

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.EmailAlreadyTaken, result.InternalErrorCode);
    }

    [Fact]
    public async Task UpdateProfileAsync_는_닉네임과_이메일을_동시에_변경한다()
    {
        // given
        var (sessionId, user) = await CreateTestUserAndSessionAsync();
        var newNickname = "UpdatedNick";
        var newEmail = "updated@example.com";

        // when
        var result = await _userService.UpdateProfileAsync(sessionId, newNickname, newEmail);

        // then
        Assert.True(result.IsSuccess);
        Assert.Equal(newNickname, result.Value!.NickName);
        Assert.Equal(newEmail, result.Value!.Email);

        var updatedUser = await _userRepository.GetByIdAsync(user.UserId);
        Assert.Equal(newNickname, updatedUser!.NickName);
        Assert.Equal(newEmail, updatedUser!.Email);
    }

    [Fact]
    public async Task UpdateProfileAsync_는_입력값이_비어있으면_실패한다()
    {
        // given
        var (sessionId, _) = await CreateTestUserAndSessionAsync();

        // when
        var result = await _userService.UpdateProfileAsync(sessionId, "", "test@test.com");

        // then
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }
}
