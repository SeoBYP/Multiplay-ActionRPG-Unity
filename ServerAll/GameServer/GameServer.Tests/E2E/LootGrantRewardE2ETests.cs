using System.Text.Json;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Common.MessageQueue;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Tests.E2E;

/// <summary>
/// 줍기 확정 → 인벤토리 지급 전 경로 E2E (실제 Redis Stream + Consumer Group + 실제 Postgres).
///
/// SocketServer가 발행하는 형식 그대로(stream:game:loot:pickup 의 "data" 필드 JSON) 메시지를 스트림에 넣고,
/// 실제 LootPickupMessageQueue(Consumer Group) 를 구독하는 실제 LootGrantConsumer 가
/// GrantItemAsync 로 DB inventory_items 에 영속 지급하는지 검증한다.
/// (LootGrantConsumerIntegrationTests 는 InMemory 큐 — 여기선 실 Redis 스트림 직렬화/그룹 경로까지 관통.)
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class LootGrantRewardE2ETests
{
    private const string StreamKey = "stream:game:loot:pickup";

    private readonly RepositoryTestFixture _fixture;

    public LootGrantRewardE2ETests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private LootGrantConsumer BuildConsumer()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(_fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(_fixture.DbConnectionString));
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var queue = new LootPickupMessageQueue(_fixture.RedisConnection, NullLogger<LootPickupMessageQueue>.Instance);
        return new LootGrantConsumer(queue, _fixture.RedisConnection, scopeFactory,
            NullLogger<LootGrantConsumer>.Instance);
    }

    private async Task<long> CreateUserAsync()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();
        return user.UserId;
    }

    private async Task PublishPickupAsync(ItemPickedUpMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await _fixture.RedisConnection.GetDatabase().StreamAddAsync(StreamKey, "data", json);
    }

    private async Task<int?> GetQuantityAsync(long userId, string itemId)
    {
        using var ctx = _fixture.CreateDbContext();
        var row = await ctx.InventoryItems.AsNoTracking()
            .SingleOrDefaultAsync(i => i.UserId == userId && i.ItemId == itemId);
        return row?.Quantity;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (await condition()) return;
            await Task.Delay(50, ct);
        }
        ct.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task 줍기_이벤트를_Redis스트림으로_발행하면_DB에_아이템이_지급된다()
    {
        long userId = await CreateUserAsync();
        var consumer = BuildConsumer();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await consumer.StartAsync(cts.Token);

        await PublishPickupAsync(new ItemPickedUpMessage
        {
            UserId = userId,
            ItemId = "potion_hp_small",
            Qty = 2,
            PickupId = $"e2e-{userId}:1",
        });

        await WaitUntilAsync(async () => await GetQuantityAsync(userId, "potion_hp_small") == 2, cts.Token);

        Assert.Equal(2, await GetQuantityAsync(userId, "potion_hp_small"));

        await consumer.StopAsync(CancellationToken.None);
    }
}
