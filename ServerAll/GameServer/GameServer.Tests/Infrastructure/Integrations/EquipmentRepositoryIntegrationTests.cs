using GameServer.Domain.Entities.Equipment;
using Shared.Gameplay.Equipment;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.Equipment;
using GameServer.Infrastructure.Domains.User;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameServer.Tests.Infrastructure.Integrations;

[Collection("RepositoryIntegrationTests")]
public class EquipmentRepositoryIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public EquipmentRepositoryIntegrationTests(RepositoryTestFixture fixture)
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

    private EquipmentRepository CreateRepository()
    {
        var context = _fixture.CreateDbContext();
        return new EquipmentRepository(_fixture.RedisConnection, context, NullLogger<EquipmentRepository>.Instance);
    }

    [Fact]
    public async Task Set_없던_슬롯이면_row를_생성한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.SetAsync(userId, EquipmentType.Weapon, "sword_basic");

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.UserEquipments.FindAsync(userId, EquipmentType.Weapon);
        Assert.NotNull(dbRow);
        Assert.Equal("sword_basic", dbRow!.ItemId);
    }

    [Fact]
    public async Task Set_같은_슬롯_재설정이면_itemId가_교체된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.SetAsync(userId, EquipmentType.Weapon, "sword_basic");
        await repository.SetAsync(userId, EquipmentType.Weapon, "armor_leather"); // 비현실 조합이지만 upsert 교체만 검증

        var equipped = await repository.GetEquippedAsync(userId);
        Assert.Single(equipped);
        Assert.Equal("armor_leather", equipped[0].ItemId);
    }

    [Fact]
    public async Task Set_후_캐시는_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.SetAsync(userId, EquipmentType.Weapon, "sword_basic");
        await repository.GetEquippedAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));

        await repository.SetAsync(userId, EquipmentType.Armor, "armor_leather");

        Assert.False(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));
    }

    [Fact]
    public async Task GetEquipped_캐시히트면_DB없이_반환한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        await repository.SetAsync(userId, EquipmentType.Armor, "armor_leather");
        await repository.GetEquippedAsync(userId); // 캐시 적재

        // DB row 삭제 — 캐시에서 와야 함
        using (var ctx = _fixture.CreateDbContext())
        {
            var row = await ctx.UserEquipments.FindAsync(userId, EquipmentType.Armor);
            ctx.UserEquipments.Remove(row!);
            await ctx.SaveChangesAsync();
        }

        var equipped = await repository.GetEquippedAsync(userId);

        Assert.Single(equipped);
        Assert.Equal("armor_leather", equipped[0].ItemId);
        Assert.Equal(EquipmentType.Armor, equipped[0].Slot);
    }

    [Fact]
    public async Task GetEquipped_캐시미스면_DB에서_읽고_캐시를_TTL과_함께_설정한다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.SetAsync(userId, EquipmentType.Weapon, "sword_basic"); // Set 이 캐시 DEL
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));

        var equipped = await repository.GetEquippedAsync(userId);

        Assert.Single(equipped);
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));

        var ttl = await db.KeyTimeToLiveAsync(RedisKeys.UserEquipment(userId));
        Assert.NotNull(ttl);
        Assert.True(ttl!.Value.TotalMinutes is > 0 and <= 30);
    }

    [Fact]
    public async Task Clear_착용중이면_true이고_행이_삭제되며_캐시도_삭제된다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        await repository.SetAsync(userId, EquipmentType.Weapon, "sword_basic");
        await repository.GetEquippedAsync(userId); // 캐시 적재
        Assert.True(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));

        var removed = await repository.ClearAsync(userId, EquipmentType.Weapon);

        Assert.True(removed);
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));

        using var ctx = _fixture.CreateDbContext();
        var dbRow = await ctx.UserEquipments.FindAsync(userId, EquipmentType.Weapon);
        Assert.Null(dbRow);
    }

    [Fact]
    public async Task Clear_빈_슬롯이면_false이고_변화없음()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();

        var removed = await repository.ClearAsync(userId, EquipmentType.Weapon);

        Assert.False(removed);
    }

    [Fact]
    public async Task GetEquipped_빈_착용이면_빈_리스트이고_캐시하지_않는다()
    {
        var userId = await CreateUserAsync();
        var repository = CreateRepository();
        var db = _fixture.RedisConnection.GetDatabase();

        var equipped = await repository.GetEquippedAsync(userId);

        Assert.Empty(equipped);
        Assert.False(await db.KeyExistsAsync(RedisKeys.UserEquipment(userId)));
    }
}
