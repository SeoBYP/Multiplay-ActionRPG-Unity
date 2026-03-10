using GameServer.Application.Domains.User.Interfaces;
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
    public async Task 사용자의_리프레시_토큰과_만료_시간을_업데이트하면_조회_시_해당_정보가_포함되어_반환된다()
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
    public async Task 리프레시_토큰_삭제_호출_후_사용자_조회_시_토큰_정보가_제거되어_있음을_확인한다()
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
    public async Task 가입되지_않은_사용자_ID로_리프레시_토큰_업데이트를_시도하면_실패를_반환한다()
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
    public async Task 과거의_시간을_만료_일자로_리프레시_토큰을_설정하려고_하면_예외가_발생한다()
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
    public async Task 연속적인_리프레시_토큰_업데이트_발생_시_마지막으로_설정된_최신_토큰_정보만_유지된다()
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