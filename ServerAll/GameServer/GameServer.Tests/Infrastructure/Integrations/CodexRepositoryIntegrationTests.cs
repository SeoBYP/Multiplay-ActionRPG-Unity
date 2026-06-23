using GameServer.Infrastructure.Domains.Codex;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class CodexRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public CodexRepositoryIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<long> CreateUserAsync()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();
        return user.UserId;
    }

    private CodexRepository CreateRepository()
        => new(_fixture.CreateDbContext(), NullLogger<CodexRepository>.Instance);

    [Fact]
    public async Task 첫_발견은_true_이고_행이_생성된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var added = await repository.AddDiscoveredAsync(userId, "potion_hp_small");

        Assert.True(added);
        var discovered = await repository.GetDiscoveredItemIdsAsync(userId);
        Assert.Contains("potion_hp_small", discovered);
    }

    [Fact]
    public async Task 재발견은_false_이고_행이_중복되지_않는다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddDiscoveredAsync(userId, "potion_hp_small");
        var second = await repository.AddDiscoveredAsync(userId, "potion_hp_small");

        Assert.False(second); // ON CONFLICT DO NOTHING → 0행
        Assert.Single(await repository.GetDiscoveredItemIdsAsync(userId));
    }

    [Fact]
    public async Task 여러_아이템_발견이_모두_조회된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddDiscoveredAsync(userId, "potion_hp_small");
        await repository.AddDiscoveredAsync(userId, "sword_basic");

        var discovered = await repository.GetDiscoveredItemIdsAsync(userId);
        Assert.Equal(2, discovered.Count);
    }

    [Fact]
    public async Task 발견은_유저별로_격리된다()
    {
        var userA = await CreateUserAsync();
        var userB = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddDiscoveredAsync(userA, "potion_hp_small");

        Assert.Empty(await repository.GetDiscoveredItemIdsAsync(userB));
    }
}
