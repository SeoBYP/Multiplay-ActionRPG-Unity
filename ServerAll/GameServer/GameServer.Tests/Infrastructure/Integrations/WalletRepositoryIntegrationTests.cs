using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Domains.Wallet;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class WalletRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public WalletRepositoryIntegrationTests(RepositoryTestFixture fixture)
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

    private WalletRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new WalletRepository(_fixture.RedisConnection, context, NullLogger<WalletRepository>.Instance);
    }

    [Fact]
    public async Task AddBalance_없던_지갑이면_row를_생성한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var balance = await repository.AddBalanceAsync(userId, 100);

        Assert.Equal(100, balance);

        using var assertContext = _fixture.CreateDbContext();
        var dbRow = await assertContext.UserWallets.FindAsync(userId);
        Assert.NotNull(dbRow);
        Assert.Equal(100, dbRow!.Balance);
    }

    [Fact]
    public async Task AddBalance_기존_지갑이면_누적된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddBalanceAsync(userId, 30);
        var balance = await repository.AddBalanceAsync(userId, 70);

        Assert.Equal(100, balance);
    }

    [Fact]
    public async Task AddBalance_후_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddBalanceAsync(userId, 10);
        await repository.GetBalanceAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));

        await repository.AddBalanceAsync(userId, 20);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));
    }

    [Fact]
    public async Task GetBalance_캐시히트면_DB없이_반환한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddBalanceAsync(userId, 55);
        await repository.GetBalanceAsync(userId); // 캐시 적재

        // DB row 삭제 — 캐시에서 와야 함
        using (var ctx = _fixture.CreateDbContext())
        {
            var row = await ctx.UserWallets.FindAsync(userId);
            ctx.UserWallets.Remove(row!);
            await ctx.SaveChangesAsync();
        }

        Assert.Equal(55, await repository.GetBalanceAsync(userId));
    }

    [Fact]
    public async Task GetBalance_캐시미스면_DB에서_읽고_캐시를_TTL과_함께_설정한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddBalanceAsync(userId, 40); // AddBalance 가 캐시 DEL
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));

        Assert.Equal(40, await repository.GetBalanceAsync(userId));
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));

        var ttl = await db.KeyTimeToLiveAsync(RedisKeys.UserWallet(userId));
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalMinutes is > 0 and <= 30);
    }

    [Fact]
    public async Task TrySpend_차감되고_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddBalanceAsync(userId, 100);
        await repository.GetBalanceAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));

        var remaining = await repository.TrySpendBalanceAsync(userId, 30);

        Assert.Equal(70, remaining);
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.UserWallets.FindAsync(userId);
        Assert.Equal(70, dbRow!.Balance);
    }

    [Fact]
    public async Task TrySpend_잔액보다_많으면_null이고_변화없음()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddBalanceAsync(userId, 50);

        var remaining = await repository.TrySpendBalanceAsync(userId, 60);

        Assert.Null(remaining);

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.UserWallets.FindAsync(userId);
        Assert.Equal(50, dbRow!.Balance); // 변화 없음
    }

    [Fact]
    public async Task TrySpend_미보유면_null()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var remaining = await repository.TrySpendBalanceAsync(userId, 1);

        Assert.Null(remaining);
    }

    [Fact]
    public async Task GetBalance_지갑이_없으면_0을_반환하고_캐시한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        var balance = await repository.GetBalanceAsync(userId);

        Assert.Equal(0, balance);
        // 인벤 Hash 와 달리 String "0" 은 MISS 와 구분 가능 → 0 도 캐시(폴백 트래픽 절감).
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserWallet(userId)));
    }
}
