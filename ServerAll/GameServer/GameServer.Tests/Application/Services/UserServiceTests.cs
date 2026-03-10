using GameServer.Application.Common;
using GameServer.Application.Domains.User;
using GameServer.Application.Domains.User.Interfaces;
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
    public async Task 유효한_세션으로_사용자_프로필_조회_시_정확한_사용자_정보를_반환한다()
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
    public async Task 로그아웃되었거나_존재하지_않는_세션으로_프로필_조회_시_실패한다()
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
    public async Task 유효하지_않은_세션으로_닉네임_변경_시도_시_실패한다()
    {
        var result = await _userService.SetNicknameAsync("invalid-session", "NewNick");
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidRequest, result.InternalErrorCode);
    }
    
    [Fact]
    public async Task 새로운_닉네임으로_변경_요청_시_사용자_정보와_저장소에_정상적으로_반영된다()
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
    public async Task 이미_다른_사용자가_사용_중인_닉네임으로_변경하려고_하면_실패한다()
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
    public async Task 길이가_너무_짧거나_허용되지_않는_문자가_포함된_닉네임으로_변경_시도_시_실패한다()
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
    public async Task 새로운_이메일_주소로_변경_요청_시_성공적으로_업데이트된다()
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
    public async Task 이미_가입된_다른_사용자의_이메일로_변경하려고_하면_실패한다()
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
    public async Task 닉네임과_이메일을_동시에_수정_요청_시_두_정보가_모두_성공적으로_반영된다()
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
    public async Task 프로필_업데이트_시_필수_값이_누락되면_실패한다()
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
