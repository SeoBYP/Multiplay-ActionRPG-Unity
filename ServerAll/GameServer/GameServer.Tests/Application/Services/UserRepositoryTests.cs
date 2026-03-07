using GameServer.Infrastructure.Interfaces.User;
using GameServer.Tests.Infrastructure;
using GameServer.Domain.Entities.User;

namespace GameServer.Tests.Infrastructure;

public class UserRepositoryTests
{
    private readonly IUserRepository _userRepository;

    public UserRepositoryTests()
    {
        _userRepository = new FakeUserRepository();
    }

    [Fact]
    public async Task 리프레시_토큰_업데이트_후_조회_시_토큰_정보_포함()
    {
        // Given
        var user = await _userRepository.AddAsync("hash", "test@test.com");
        var userId = user.UserId;
        var refreshToken = "some-refresh-token";
        var expiry = DateTime.UtcNow.AddDays(7);

        // When
        await _userRepository.UpdateRefreshTokenAsync(userId, refreshToken, expiry);
        var retrievedUser = await _userRepository.GetByIdAsync(userId);

        // Then
        Assert.NotNull(retrievedUser);
        Assert.Equal(refreshToken, retrievedUser.RefreshToken);
        Assert.Equal(expiry, retrievedUser.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task 리프레시_토큰_삭제_후_조회_시_토큰_정보_없음()
    {
        // Given
        var user = await _userRepository.AddAsync("hash", "test@test.com");
        var userId = user.UserId;
        var refreshToken = "some-refresh-token";
        var expiry = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateRefreshTokenAsync(userId, refreshToken, expiry);

        // When
        await _userRepository.ClearRefreshTokenAsync(userId);
        var retrievedUser = await _userRepository.GetByIdAsync(userId);

        // Then
        Assert.NotNull(retrievedUser);
        Assert.Null(retrievedUser.RefreshToken);
        Assert.Equal(default, retrievedUser.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task 존재하지_않는_사용자_리프레시_토큰_업데이트_시_실패한다()
    {
        // Given
        var nonExistentUserId = 9999L;
        var refreshToken = "some-token";
        var expiry = DateTime.UtcNow.AddDays(7);

        // When
        var result = await _userRepository.UpdateRefreshTokenAsync(nonExistentUserId, refreshToken, expiry);

        // Then
        Assert.False(result);
    }

    [Fact]
    public async Task 만료된_만료_시간으로_리프레시_토큰_업데이트_시_예외_발생()
    {
        // Given
        var user = await _userRepository.AddAsync("hash", "test@test.com");
        var userId = user.UserId;
        var refreshToken = "some-token";
        var expiredExpiry = DateTime.UtcNow.AddDays(-1);

        // When & Then
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _userRepository.UpdateRefreshTokenAsync(userId, refreshToken, expiredExpiry));
    }

    [Fact]
    public async Task 리프레시_토큰_로테이션_시_최신_토큰만_남아있는지_확인()
    {
        // Given
        var user = await _userRepository.AddAsync("hash", "test@test.com");
        var userId = user.UserId;
        
        var firstToken = "first-token";
        var firstExpiry = DateTime.UtcNow.AddDays(7);
        await _userRepository.UpdateRefreshTokenAsync(userId, firstToken, firstExpiry);

        var secondToken = "second-token";
        var secondExpiry = DateTime.UtcNow.AddDays(7);

        // When
        await _userRepository.UpdateRefreshTokenAsync(userId, secondToken, secondExpiry);
        var retrievedUser = await _userRepository.GetByIdAsync(userId);

        // Then
        Assert.NotNull(retrievedUser);
        Assert.Equal(secondToken, retrievedUser.RefreshToken);
        Assert.Equal(secondExpiry, retrievedUser.RefreshTokenExpiresAt);
    }
}