using GameServer.Application.Domains.User.Interfaces;
using GameServer.Tests.Infrastructure.Fakes.Repositories;

namespace GameServer.Tests.Application.Services;

public class UserRepositoryTests
{
    private readonly IUserCredentialRepository _credentialRepository = new FakeUserCredentialRepository();

    [Fact]
    public async Task 리프레시_토큰과_만료시간을_업데이트하면_조회_결과에_반영된다()
    {
        var credential = await _credentialRepository.CreateAsync(1, "test@test.com", "hash");
        var expiry = DateTime.UtcNow.AddDays(7);

        credential.SetRefreshToken("refresh-token", expiry);
        await _credentialRepository.UpdateAsync(credential);
        
        var updated = await _credentialRepository.FindByIdAsync(1);

        Assert.NotNull(updated);
        Assert.Equal("refresh-token", updated!.RefreshToken);
        Assert.Equal(expiry, updated.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task 리프레시_토큰_삭제_후에는_토큰_정보가_비워진다()
    {
        var credential = await _credentialRepository.CreateAsync(1, "test@test.com", "hash");
        credential.SetRefreshToken("refresh-token", DateTime.UtcNow.AddDays(7));
        await _credentialRepository.UpdateAsync(credential);

        await _credentialRepository.ClearRefreshTokenAsync(1);
        var updated = await _credentialRepository.FindByIdAsync(1);

        Assert.NotNull(updated);
        Assert.Null(updated!.RefreshToken);
        Assert.Equal(default, updated.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task 존재하지_않는_사용자_ID로_업데이트하면_false를_반환한다()
    {
        var credential = GameServer.Domain.Entities.User.UserCredential.Create(9999, "notfound@test.com", "hash");
        var result = await _credentialRepository.UpdateAsync(credential);

        Assert.False(result);
    }

    [Fact]
    public async Task 비밀번호_해시를_업데이트하면_조회_결과에_반영된다()
    {
        await _credentialRepository.CreateAsync(1, "test@test.com", "old-hash");

        var result = await _credentialRepository.UpdatePasswordHashAsync(1, "new-hash");
        var credential = await _credentialRepository.FindByIdAsync(1);

        Assert.True(result);
        Assert.Equal("new-hash", credential!.PasswordHash);
    }
}
