using GameServer.Application.Domains.Equipment;
using GameServer.Application.Domains.Equipment.Interfaces;
using GameServer.Application.Domains.Inventory;
using GameServer.Application.Domains.Inventory.Interfaces;
using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Domains;
using GameServer.Infrastructure.Domains.Equipment;
using GameServer.Infrastructure.Domains.Inventory;
using GameServer.Infrastructure.Domains.Progression;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Fakes.MessageQueue;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// 던전 클리어 → Exp 보상 지급 파이프라인 통합 테스트.
///
/// 실제 DungeonResultConsumer(BackgroundService) + 실제 Progression 스택(Postgres) + 실제 Redis(멱등)를
/// 띄우고 InMemoryMessageQueue 로 DungeonClearMessage 를 흘려 "참가자 전원 Exp 적립 + RoomId 멱등"을 검증한다.
/// 던전별 보상은 Shared 카탈로그(spawn-layouts.json 의 expReward) — dungeon_01 = 100.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class DungeonResultConsumerIntegrationTests
{
    private readonly RepositoryTestFixture _fixture;

    public DungeonResultConsumerIntegrationTests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required ServiceProvider Provider { get; init; }
        public required InMemoryMessageQueue<DungeonClearMessage> Queue { get; init; }
        public required DungeonResultConsumer Consumer { get; init; }

        public ValueTask DisposeAsync() => Provider.DisposeAsync();
    }

    /// <summary>지정한 유저의 첫 AddExp 만 던지는 데코레이터 — 부분 실패 재현용.</summary>
    private sealed class FailOnceProgression(
        IProgressionService inner, long failUserId, System.Runtime.CompilerServices.StrongBox<int> fired) : IProgressionService
    {
        public Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default)
        {
            // 컨슈머가 참가자마다 새 스코프를 열므로 "한 번만 실패" 상태는 스코프 밖에서 공유해야 한다.
            if (userId == failUserId && Interlocked.Exchange(ref fired.Value, 1) == 0)
                throw new InvalidOperationException("transient db failure");
            return inner.AddExpAsync(userId, amount, ct);
        }

        public Task<GameServer.Domain.Entities.User.UserProgression> GetProgressionAsync(long userId, CancellationToken ct = default)
            => inner.GetProgressionAsync(userId, ct);

        public Task<PlayerStats> GetStatsAsync(long userId, CancellationToken ct = default)
            => inner.GetStatsAsync(userId, ct);
    }

    private Harness BuildHarness(long failExpForUserId = 0)
    {
        var queue = new InMemoryMessageQueue<DungeonClearMessage>();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(_fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(_fixture.DbConnectionString));
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IProgressionService, ProgressionService>();
        // ProgressionService.GetStatsAsync 가 장비 합산(3.2)을 위임 → 생성자 의존 체인 등록(보상=AddExp 만 쓰지만 DI 해석 필요).
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();
        // 도감(3.7): InventoryService.GrantItemAsync 가 ICodexService 의존 → DI 해석 위해 등록.
        services.AddScoped<GameServer.Application.Domains.Codex.Interfaces.ICodexRepository, GameServer.Infrastructure.Domains.Codex.CodexRepository>();
        services.AddScoped<GameServer.Application.Domains.Codex.Interfaces.ICodexService, GameServer.Application.Domains.Codex.CodexService>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<GameServer.Application.Domains.Reward.Interfaces.IRewardLedger, GameServer.Infrastructure.Domains.Reward.RewardLedger>();
        if (failExpForUserId != 0)
        {
            var firedOnce = new System.Runtime.CompilerServices.StrongBox<int>(0);
            services.AddScoped<IProgressionService>(sp => new FailOnceProgression(
                new ProgressionService(
                    sp.GetRequiredService<IProgressionRepository>(),
                    sp.GetRequiredService<IEquipmentService>()),
                failExpForUserId,
                firedOnce));
        }
        services.AddSingleton<IMessageQueue<DungeonClearMessage>>(queue);
        services.AddSingleton<DungeonResultConsumer>();

        var provider = services.BuildServiceProvider();
        return new Harness
        {
            Provider = provider,
            Queue = queue,
            Consumer = provider.GetRequiredService<DungeonResultConsumer>(),
        };
    }

    private async Task<(int Level, long Exp)?> GetProgAsync(long userId)
    {
        using var ctx = _fixture.CreateDbContext();
        var row = await ctx.UserProgressions.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
        return row is null ? null : (row.Level, row.Exp);
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
    public async Task 클리어_소비시_참가자_전원에게_던전Exp를_지급하고_임계를_넘으면_레벨업한다()
    {
        await using var h = BuildHarness();
        long expReward = SpawnLayoutTable.Get(MapIds.Dungeon01).ExpReward; // 100 = Lv1 임계 정확 → Lv2/Exp0
        long u1 = 7001, u2 = 7002;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new DungeonClearMessage
        {
            RoomId = 70001,
            MapId = MapIds.Dungeon01,
            Participants = [u1, u2],
        });

        await WaitUntilAsync(async () => (await GetProgAsync(u1))?.Level == 2 && (await GetProgAsync(u2))?.Level == 2, cts.Token);

        // 던전 보상 Exp 가 실제 레벨업으로 이어져 DB 에 영속됨(경험치→레벨업, 던전 경로).
        Assert.Equal((2, 0L), await GetProgAsync(u1));
        Assert.Equal((2, 0L), await GetProgAsync(u2));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task 같은_RoomId_재전달이면_이중지급하지_않는다()
    {
        await using var h = BuildHarness();
        long u1 = 7101;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await h.Consumer.StartAsync(cts.Token);

        var msg = new DungeonClearMessage { RoomId = 71001, MapId = MapIds.Dungeon01, Participants = [u1] };
        await h.Queue.EnqueueAsync(msg);
        await WaitUntilAsync(async () => (await GetProgAsync(u1))?.Level == 2, cts.Token); // 100 → Lv2/Exp0

        // 동일 RoomId 재전달 — 멱등이라 추가 지급 없어야 함(이중지급이면 Lv2/Exp100 이 됨).
        await h.Queue.EnqueueAsync(msg);
        await Task.Delay(300, cts.Token); // 처리 기회를 준 뒤

        Assert.Equal((2, 0L), await GetProgAsync(u1));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    private async Task<List<(string Key, string Kind, long Amount)>> LedgerRowsAsync(long roomId)
    {
        await using var ctx = _fixture.CreateDbContext();
        var prefix = $"dungeon:{roomId}:";
        return await ctx.RewardGrants.AsNoTracking()
            .Where(g => g.GrantKey.StartsWith(prefix))
            .OrderBy(g => g.GrantKey)
            .Select(g => new ValueTuple<string, string, long>(g.GrantKey, g.Kind, g.Amount))
            .ToListAsync();
    }

    [Fact]
    public async Task 지급_기록이_방_참가자별로_원장에_남는다()
    {
        long u1 = 7201, u2 = 7202, roomId = 72001;
        await using var h = BuildHarness();
        long expReward = SpawnLayoutTable.Get(MapIds.Dungeon01).ExpReward;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await h.Consumer.StartAsync(cts.Token);

        await h.Queue.EnqueueAsync(new DungeonClearMessage
        {
            RoomId = roomId, MapId = MapIds.Dungeon01, Participants = [u1, u2],
        });
        await WaitUntilAsync(async () => (await LedgerRowsAsync(roomId)).Count == 2, cts.Token);

        // 멱등 단위가 "메시지" 가 아니라 "참가자" 다 — 그래야 부분 실패 후 나머지만 마저 줄 수 있다.
        var rows = await LedgerRowsAsync(roomId);
        Assert.Equal(
            new[] { $"dungeon:{roomId}:{u1}", $"dungeon:{roomId}:{u2}" },
            rows.Select(r => r.Key).ToArray());
        Assert.All(rows, r => Assert.Equal("exp", r.Kind));
        Assert.All(rows, r => Assert.Equal(expReward, r.Amount));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task 원장은_만료되지_않아_아무리_늦게_재배달돼도_이중지급되지_않는다()
    {
        long u1 = 7301, roomId = 73001;
        await using var h = BuildHarness();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await h.Consumer.StartAsync(cts.Token);

        var msg = new DungeonClearMessage { RoomId = roomId, MapId = MapIds.Dungeon01, Participants = [u1] };
        await h.Queue.EnqueueAsync(msg);
        await WaitUntilAsync(async () => (await GetProgAsync(u1))?.Level == 2, cts.Token);

        // 예전 Redis 집합은 TTL 이 있어 "무이벤트 24h → 기록 전멸 → 재배달 시 이중지급" 이 가능했다.
        // 원장 행에는 TTL 이 없다 — 시간이 얼마가 지나든 같은 GrantKey 는 다시 지급되지 않는다.
        await h.Queue.EnqueueAsync(msg);
        await Task.Delay(400, cts.Token);

        Assert.Equal((2, 0L), await GetProgAsync(u1));
        Assert.Single(await LedgerRowsAsync(roomId));

        await h.Consumer.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task 참가자_일부_지급_후_실패해도_재시도하면_나머지만_지급된다()
    {
        long u1 = 9601, u2 = 9602, u3 = 9603;
        await using var h = BuildHarness(failExpForUserId: u2);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await h.Consumer.StartAsync(cts.Token);

        var msg = new DungeonClearMessage { RoomId = 96100, MapId = MapIds.Dungeon01, Participants = [u1, u2, u3] };

        // 1차: u1 지급 → u2 에서 예외 → u3 는 아예 시도되지 않는다.
        await h.Queue.EnqueueAsync(msg);
        await WaitUntilAsync(async () => (await GetProgAsync(u1))?.Level == 2, cts.Token);
        await Task.Delay(300, cts.Token);
        Assert.Null(await GetProgAsync(u3)); // 아직 못 받았다

        // 2차: 재배달 재현. 이미 받은 u1 은 원장이 막고, u2·u3 만 마저 받아야 한다.
        await h.Queue.EnqueueAsync(msg);
        await WaitUntilAsync(async () =>
            (await GetProgAsync(u2))?.Level == 2 && (await GetProgAsync(u3))?.Level == 2, cts.Token);
        await Task.Delay(300, cts.Token);

        // 셋 다 정확히 한 번씩(100 = Lv1 임계 정확 → Lv2/Exp0). u1 이 두 번 받았으면 Exp 가 남는다.
        Assert.Equal((2, 0L), await GetProgAsync(u1));
        Assert.Equal((2, 0L), await GetProgAsync(u2));
        Assert.Equal((2, 0L), await GetProgAsync(u3));

        await h.Consumer.StopAsync(CancellationToken.None);
    }
}
