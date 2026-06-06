using System.Text.Json;
using GameServer.Application.Domains.Progression;
using GameServer.Application.Domains.Progression.Interfaces;
using GameServer.Infrastructure.Common.Consumer;
using GameServer.Infrastructure.Common.MessageQueue;
using GameServer.Infrastructure.Domains.Progression;
using GameServer.Infrastructure.Persistence;
using GameServer.Tests.Infrastructure.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using StackExchange.Redis;

namespace GameServer.Tests.E2E;

/// <summary>
/// 던전 클리어 → 보상 지급 전 경로 E2E (실제 Redis Stream + Consumer Group + 실제 Postgres).
///
/// SocketServer가 발행하는 형식 그대로(stream:game:dungeon:result 의 "data" 필드 JSON) 메시지를 스트림에 넣고,
/// 실제 DungeonClearMessageQueue(Consumer Group) 를 구독하는 실제 DungeonResultConsumer 가
/// Shared 카탈로그(dungeon_01 expReward=100)로 참가자 전원에게 Exp 를 적립해 DB 에 영속하는지 검증한다.
/// (DungeonResultConsumerIntegrationTests 는 InMemory 큐 — 여기선 실 Redis 스트림 직렬화/그룹 경로까지 관통.)
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class DungeonResultRewardE2ETests
{
    private const string StreamKey = "stream:game:dungeon:result";

    private readonly RepositoryTestFixture _fixture;

    public DungeonResultRewardE2ETests(RepositoryTestFixture fixture)
    {
        _fixture = fixture;
    }

    private DungeonResultConsumer BuildConsumer()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConnectionMultiplexer>(_fixture.RedisConnection);
        services.AddDbContext<GameServerDbContext>(o => o.UseNpgsql(_fixture.DbConnectionString));
        services.AddScoped<IProgressionRepository, ProgressionRepository>();
        services.AddScoped<IProgressionService, ProgressionService>();

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        var queue = new DungeonClearMessageQueue(_fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance);
        return new DungeonResultConsumer(queue, _fixture.RedisConnection, scopeFactory,
            NullLogger<DungeonResultConsumer>.Instance);
    }

    private async Task PublishClearAsync(DungeonClearMessage message)
    {
        var json = JsonSerializer.Serialize(message);
        await _fixture.RedisConnection.GetDatabase().StreamAddAsync(StreamKey, "data", json);
    }

    private async Task<long?> GetExpAsync(long userId)
    {
        using var ctx = _fixture.CreateDbContext();
        var row = await ctx.UserProgressions.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId);
        return row?.Exp;
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
    public async Task 클리어_이벤트를_Redis스트림으로_발행하면_참가자_전원_DB에_Exp가_지급된다()
    {
        long expected = SpawnLayoutTable.Get(MapIds.Dungeon01).ExpReward; // 100
        long u1 = 7201, u2 = 7202;
        var consumer = BuildConsumer();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await consumer.StartAsync(cts.Token);

        await PublishClearAsync(new DungeonClearMessage
        {
            RoomId = 72001,
            MapId = MapIds.Dungeon01,
            Participants = [u1, u2],
        });

        await WaitUntilAsync(async () => await GetExpAsync(u1) == expected && await GetExpAsync(u2) == expected, cts.Token);

        Assert.Equal(expected, await GetExpAsync(u1));
        Assert.Equal(expected, await GetExpAsync(u2));

        await consumer.StopAsync(CancellationToken.None);
    }
}
