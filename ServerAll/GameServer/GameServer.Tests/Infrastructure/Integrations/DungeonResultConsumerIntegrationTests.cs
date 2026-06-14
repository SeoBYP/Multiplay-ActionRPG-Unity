using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Domains;
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

    private Harness BuildHarness()
    {
        var queue = new InMemoryMessageQueue<DungeonClearMessage>();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(_fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(_fixture.DbConnectionString));
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IProgressionService, ProgressionService>();
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
}
