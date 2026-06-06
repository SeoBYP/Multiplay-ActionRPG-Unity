using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.Progression;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class ProgressionRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public ProgressionRepositoryIntegrationTests(RepositoryTestFixture fixture)
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

    private ProgressionRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new ProgressionRepository(_fixture.RedisConnection, context, NullLogger<ProgressionRepository>.Instance);
    }

    [Fact]
    public async Task AddExp_없던_유저면_row를_생성하고_적립한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var result = await repository.AddExpAsync(userId, 120);

        Assert.Equal(120L, result.Exp);

        using var assertContext = _fixture.CreateDbContext();
        var dbRow = await assertContext.UserProgressions.FindAsync(userId);
        Assert.NotNull(dbRow);
        Assert.Equal(120L, dbRow.Exp);
        Assert.Equal(1, dbRow.Level);
    }

    [Fact]
    public async Task AddExp_기존_유저면_누적된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddExpAsync(userId, 50);
        var result = await repository.AddExpAsync(userId, 30);

        Assert.Equal(80L, result.Exp);

        using var assertContext = _fixture.CreateDbContext();
        var dbRow = await assertContext.UserProgressions.FindAsync(userId);
        Assert.Equal(80L, dbRow?.Exp);
    }

    [Fact]
    public async Task AddExp_후_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        // Get 으로 캐시 적재 → AddExp 가 DEL 하는지 확인 (Cache-Aside Delete 계약)
        await repository.AddExpAsync(userId, 10);
        await repository.GetAsync(userId);
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserProgression(userId)));

        await repository.AddExpAsync(userId, 5);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProgression(userId)));
    }

    [Fact]
    public async Task Get_캐시히트면_DB없이_반환한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddExpAsync(userId, 70);
        await repository.GetAsync(userId); // 캐시 적재

        // DB row 삭제 — 캐시에서 와야 함
        using (var ctx = _fixture.CreateDbContext())
        {
            var row = await ctx.UserProgressions.FindAsync(userId);
            ctx.UserProgressions.Remove(row!);
            await ctx.SaveChangesAsync();
        }

        var found = await repository.GetAsync(userId);

        Assert.NotNull(found);
        Assert.Equal(70L, found.Exp);
    }

    [Fact]
    public async Task Get_캐시미스면_DB에서_읽고_캐시를_설정한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddExpAsync(userId, 40); // AddExp 가 캐시 DEL → 현재 캐시 없음
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserProgression(userId)));

        var found = await repository.GetAsync(userId);

        Assert.NotNull(found);
        Assert.Equal(40L, found.Exp);
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserProgression(userId)));

        var ttl = await db.KeyTimeToLiveAsync(RedisKeys.UserProgression(userId));
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalMinutes is > 0 and <= 30);
    }

    [Fact]
    public async Task Get_row가_없으면_null을_반환한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var found = await repository.GetAsync(userId);

        Assert.Null(found);
    }
}
