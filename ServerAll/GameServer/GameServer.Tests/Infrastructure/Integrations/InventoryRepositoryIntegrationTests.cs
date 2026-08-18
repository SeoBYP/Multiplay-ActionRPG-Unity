using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class InventoryRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public InventoryRepositoryIntegrationTests(RepositoryTestFixture fixture)
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

    private InventoryRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new InventoryRepository(_fixture.RedisConnection, context, NullLogger<InventoryRepository>.Instance);
    }

    [Fact]
    public async Task AddQuantity_없던_아이템이면_row를_생성한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var result = await repository.AddQuantityAsync(userId, 1001, 3, maxStack: 99);

        Assert.Equal(3, result.Quantity);

        using var assertContext = _fixture.CreateDbContext();
        var dbRow = await assertContext.InventoryItems.FindAsync(userId, 1001);
        Assert.NotNull(dbRow);
        Assert.Equal(3, dbRow!.Quantity);
    }

    [Fact]
    public async Task AddQuantity_기존_아이템이면_누적된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddQuantityAsync(userId, 1001, 2, maxStack: 99);
        var result = await repository.AddQuantityAsync(userId, 1001, 5, maxStack: 99);

        Assert.Equal(7, result.Quantity);
    }

    [Fact]
    public async Task AddQuantity_MaxStack을_넘으면_상한에서_멈춘다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var result = await repository.AddQuantityAsync(userId, 1001, 150, maxStack: 99);

        Assert.Equal(99, result.Quantity);
    }

    [Fact]
    public async Task AddQuantity_후_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddQuantityAsync(userId, 1001, 1, maxStack: 99);
        await repository.GetAllAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));

        await repository.AddQuantityAsync(userId, 1001, 2, maxStack: 99);

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));
    }

    [Fact]
    public async Task GetAll_캐시히트면_DB없이_반환한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddQuantityAsync(userId, 1002, 5, maxStack: 99);
        await repository.GetAllAsync(userId); // 캐시 적재

        // DB row 삭제 — 캐시에서 와야 함
        using (var ctx = _fixture.CreateDbContext())
        {
            var row = await ctx.InventoryItems.FindAsync(userId, 1002);
            ctx.InventoryItems.Remove(row!);
            await ctx.SaveChangesAsync();
        }

        var items = await repository.GetAllAsync(userId);

        Assert.Single(items);
        Assert.Equal(5, items[0].Quantity);
    }

    [Fact]
    public async Task GetAll_캐시미스면_DB에서_읽고_캐시를_TTL과_함께_설정한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddQuantityAsync(userId, 1001, 4, maxStack: 99); // AddQty 가 캐시 DEL
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));

        var items = await repository.GetAllAsync(userId);

        Assert.Single(items);
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));

        var ttl = await db.KeyTimeToLiveAsync(RedisKeys.UserInventory(userId));
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalMinutes is > 0 and <= 30);
    }

    [Fact]
    public async Task RemoveQuantity_차감되고_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.AddQuantityAsync(userId, 1001, 5, maxStack: 99);
        await repository.GetAllAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));

        var remaining = await repository.RemoveQuantityAsync(userId, 1001, 2);

        Assert.Equal(3, remaining);
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.InventoryItems.FindAsync(userId, 1001);
        Assert.Equal(3, dbRow!.Quantity);
    }

    [Fact]
    public async Task RemoveQuantity_전량차감하면_행이_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddQuantityAsync(userId, 1001, 2, maxStack: 99);

        var remaining = await repository.RemoveQuantityAsync(userId, 1001, 2);

        Assert.Equal(0, remaining);

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.InventoryItems.FindAsync(userId, 1001);
        Assert.Null(dbRow); // 0 스택 → 행 삭제
    }

    [Fact]
    public async Task RemoveQuantity_보유보다_많으면_null이고_변화없음()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.AddQuantityAsync(userId, 1001, 2, maxStack: 99);

        var remaining = await repository.RemoveQuantityAsync(userId, 1001, 5);

        Assert.Null(remaining);

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.InventoryItems.FindAsync(userId, 1001);
        Assert.Equal(2, dbRow!.Quantity); // 변화 없음
    }

    [Fact]
    public async Task RemoveQuantity_미보유면_null()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var remaining = await repository.RemoveQuantityAsync(userId, 1001, 1);

        Assert.Null(remaining);
    }

    [Fact]
    public async Task GetAll_빈_인벤토리면_빈_리스트이고_캐시하지_않는다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        var items = await repository.GetAllAsync(userId);

        Assert.Empty(items);
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserInventory(userId)));
    }
}
