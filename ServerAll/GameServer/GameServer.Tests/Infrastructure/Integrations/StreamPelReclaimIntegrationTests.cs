using System.Text.Json;
using GameServer.Infrastructure.Common.MessageQueue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// Consumer Group PEL(Pending Entry List) 자동 회수 검증 (F4).
///
/// ACK 전에 컨슈머가 죽으면 그 메시지는 그 컨슈머의 PEL 에 남는다. 회수 주체가 없으면 **영구 잔류**한다 —
/// 특히 컨슈머 이름이 매 기동 바뀌면 자기 PEL 재읽기("0")도 소용이 없다.
/// 실 Redis(Testcontainers)에 "죽은 컨슈머" 를 만들어 두고, 산 컨슈머가 그것을 집어오는지 본다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class StreamPelReclaimIntegrationTests(RepositoryTestFixture fixture)
{
    private const string StreamKey = "stream:game:dungeon:result";
    private const string GroupName = "dungeon-result-service";

    /// <summary>회수 파라미터만 테스트용으로 압축한 실제 큐(로직은 그대로).</summary>
    private sealed class FastSweepQueue(IConnectionMultiplexer redis, ILogger<DungeonClearMessageQueue> logger)
        : DungeonClearMessageQueue(redis, logger)
    {
        protected override TimeSpan PendingMinIdle => TimeSpan.FromMilliseconds(200);
        protected override TimeSpan AutoClaimInterval => TimeSpan.FromMilliseconds(100);
        protected override TimeSpan IdlePollDelay => TimeSpan.FromMilliseconds(50);
    }

    private async Task ResetStreamAsync()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await db.KeyDeleteAsync(StreamKey);
        await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.Beginning, createStream: true);
    }

    private static async Task<T?> FirstOrTimeoutAsync<T>(IAsyncEnumerable<T> source, TimeSpan timeout)
        where T : class
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await foreach (var item in source.WithCancellation(cts.Token))
                return item;
        }
        catch (OperationCanceledException)
        {
            // 시간 안에 아무것도 안 왔다 = 회수되지 않았다
        }
        return null;
    }

    [Fact]
    public async Task ACK_전에_죽은_컨슈머의_메시지를_회수해_다시_처리한다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await ResetStreamAsync();

        var payload = JsonSerializer.Serialize(new DungeonClearMessage
        {
            RoomId = 88001,
            MapId = MapIds.Dungeon01,
            Participants = [8801],
        });
        await db.StreamAddAsync(StreamKey, "data", payload);

        // 죽은 컨슈머 재현 — 읽어가서 PEL 에 넣고 ACK 하지 않는다(그리고 다시는 돌아오지 않는다).
        var stolen = await db.StreamReadGroupAsync(StreamKey, GroupName, "dead-consumer", ">", count: 10);
        Assert.Single(stolen);

        await Task.Delay(400); // PendingMinIdle 경과

        var queue = new FastSweepQueue(fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance);
        var received = await FirstOrTimeoutAsync(queue.DequeueAllAsync(CancellationToken.None), TimeSpan.FromSeconds(5));

        Assert.NotNull(received);
        Assert.Equal(88001, received.Payload.RoomId);

        // 회수는 소유권만 옮긴다 — ACK 는 아직이다(핸들러가 성공해야 ACK, at-least-once).
        var beforeAck = await db.StreamPendingAsync(StreamKey, GroupName);
        Assert.Equal(1, beforeAck.PendingMessageCount);

        // 소비자가 처리에 성공해 ACK 하면 그제서야 PEL 에서 빠진다(다음 스윕에서 또 집지 않는다).
        await received.AcknowledgeAsync();
        var afterAck = await db.StreamPendingAsync(StreamKey, GroupName);
        Assert.Equal(0, afterAck.PendingMessageCount);
    }

    [Fact]
    public async Task 살아있는_컨슈머가_방금_집어간_메시지는_빼앗지_않는다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await ResetStreamAsync();

        var payload = JsonSerializer.Serialize(new DungeonClearMessage
        {
            RoomId = 88002,
            MapId = MapIds.Dungeon01,
            Participants = [8802],
        });
        await db.StreamAddAsync(StreamKey, "data", payload);

        // 다른 컨슈머가 방금 집어갔다(아직 유휴 시간이 안 찼다 = 처리 중일 수 있다).
        var taken = await db.StreamReadGroupAsync(StreamKey, GroupName, "busy-consumer", ">", count: 10);
        Assert.Single(taken);

        // MinIdle(200ms)보다 짧게만 기다리고 회수를 돌린다 → 빼앗으면 안 된다.
        var queue = new FastSweepQueue(fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance);
        var received = await FirstOrTimeoutAsync(queue.DequeueAllAsync(CancellationToken.None), TimeSpan.FromMilliseconds(150));

        Assert.Null(received);
    }

    [Fact]
    public async Task 역직렬화_실패_엔트리는_ACK_돼_회수_루프를_돌지_않는다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await ResetStreamAsync();

        // 깨진 페이로드를 죽은 컨슈머의 PEL 에 남긴다.
        await db.StreamAddAsync(StreamKey, "data", "{ not json");
        var stolen = await db.StreamReadGroupAsync(StreamKey, GroupName, "dead-consumer", ">", count: 10);
        Assert.Single(stolen);

        await Task.Delay(400);

        var queue = new FastSweepQueue(fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance);
        // 정상 메시지는 없으므로 아무것도 안 나온다. 목적은 "독이 PEL 에서 치워졌는가".
        await FirstOrTimeoutAsync(queue.DequeueAllAsync(CancellationToken.None), TimeSpan.FromSeconds(2));

        var pending = await db.StreamPendingAsync(StreamKey, GroupName);
        Assert.Equal(0, pending.PendingMessageCount);
    }
}
