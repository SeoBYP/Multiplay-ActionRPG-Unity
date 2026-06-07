using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.User;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Fakes.MessageQueue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// 줍기 확정 → 인벤토리 영속 지급 파이프라인 통합 테스트.
///
/// 실제 LootGrantConsumer(BackgroundService) + 실제 Inventory 스택(Postgres) + 실제 Redis(멱등)를
/// 띄우고 InMemoryMessageQueue 로 ItemPickedUpMessage 를 흘려 "지급(Create/Update) + PickupId 멱등"을 검증한다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class LootGrantConsumerIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public LootGrantConsumerIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required InMemoryMessageQueue<ItemPickedUpMessage> Queue { get; init; }
        public required LootGrantConsumer Consumer { get; init; }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    private Harness BuildHarness()
    {
        var queue = new InMemoryMessageQueue<ItemPickedUpMessage>();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(_fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(_fixture.DbConnectionString));
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddSingleton<IMessageQueue<ItemPickedUpMessage>>(queue);
        services.AddSingleton<LootGrantConsumer>();

        var provider = services.BuildServiceProvider();
        return new Harness
        {
            Provider = provider,
            Queue = queue,
            Consumer = provider.GetRequiredService<LootGrantConsumer>(),
        };
    }

    private async Task<long> CreateUserAsync()
    {
        using var context = _fixture.CreateDbContext();
        var userRepo = new UserRepository(_fixture.RedisConnection, context, NullLogger<UserRepository>.Instance);
        var user = await userRepo.CreateAsync();
        return user.UserId;
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
            await Task.Delay(20, ct);
        }
        ct.ThrowIfCancellationRequested();
    }

    [Fact]
    public async Task 줍기_소비시_DB에_아이템이_지급된다()
    {
        await using var h = BuildHarness();
        long userId = await CreateUserAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new ItemPickedUpMessage
        {
            UserId = userId,
            ItemId = "potion_hp_small",
            Qty = 3,
            PickupId = "9001:1",
        });

        await WaitUntilAsync(async () => await GetQuantityAsync(userId, "potion_hp_small") == 3, cts.Token);

        Assert.Equal(3, await GetQuantityAsync(userId, "potion_hp_small"));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task 같은_PickupId_재전달이면_이중지급하지_않는다()
    {
        await using var h = BuildHarness();
        long userId = await CreateUserAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await h.Consumer.StartAsync(cts.Token);

        var msg = new ItemPickedUpMessage
        {
            UserId = userId,
            ItemId = "gold_pouch",
            Qty = 2,
            PickupId = "9101:1",
        };
        await h.Queue.EnqueueAsync(msg);
        await WaitUntilAsync(async () => await GetQuantityAsync(userId, "gold_pouch") == 2, cts.Token);

        // 동일 PickupId 재전달 — 멱등이라 추가 지급 없어야 함.
        await h.Queue.EnqueueAsync(msg);
        await Task.Delay(300, cts.Token); // 처리 기회를 준 뒤

        Assert.Equal(2, await GetQuantityAsync(userId, "gold_pouch"));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task 미존재_itemId는_지급되지_않고_소비는_죽지_않는다()
    {
        await using var h = BuildHarness();
        long userId = await CreateUserAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new ItemPickedUpMessage
        {
            UserId = userId, ItemId = "no_such_item", Qty = 1, PickupId = "9201:1",
        });
        // 뒤따르는 정상 메시지가 처리되면 = 소비 루프가 살아있고 미존재 itemId 는 그냥 스킵됐다는 증거.
        await h.Queue.EnqueueAsync(new ItemPickedUpMessage
        {
            UserId = userId, ItemId = "potion_hp_small", Qty = 1, PickupId = "9201:2",
        });

        await WaitUntilAsync(async () => await GetQuantityAsync(userId, "potion_hp_small") == 1, cts.Token);

        Assert.Null(await GetQuantityAsync(userId, "no_such_item"));

        await h.Consumer.StopAsync(CancellationToken.None);
    }
}
