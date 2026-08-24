using System.Text.Json;
using GameServer.Infrastructure.Common.MessageQueue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using StackExchange.Redis;

namespace GameServer.Tests.Infrastructure.Integrations;

/// <summary>
/// at-least-once 전달 검증 — 핸들러가 실패한 메시지는 ACK 되지 않고 재배달돼 다시 시도된다.
///
/// 이전(at-most-once)에는 ACK 가 핸들러보다 앞서서, DB 순단 한 번이 곧 메시지 영구 소실이었다.
/// 함께 검증하는 것이 **재시도 상한** 이다 — 상한이 없으면 항상 실패하는 메시지가 영원히 재시도된다.
/// </summary>
[Collection("RepositoryIntegrationTests")]
public class StreamRedeliveryIntegrationTests(RepositoryTestFixture fixture)
{
    private const string StreamKey = "stream:game:dungeon:result";
    private const string GroupName = "dungeon-result-service";

    /// <summary>회수 파라미터·재시도 상한만 압축한 실제 큐(로직은 프로덕션 그대로).</summary>
    private sealed class FastSweepQueue(IConnectionMultiplexer redis, ILogger<DungeonClearMessageQueue> logger, int maxAttempts)
        : DungeonClearMessageQueue(redis, logger)
    {
        protected override TimeSpan PendingMinIdle => TimeSpan.FromMilliseconds(150);
        protected override TimeSpan AutoClaimInterval => TimeSpan.FromMilliseconds(50);
        protected override TimeSpan IdlePollDelay => TimeSpan.FromMilliseconds(30);
        protected override int MaxDeliveryAttempts => maxAttempts;
    }

    private async Task ResetStreamAsync()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await db.KeyDeleteAsync(StreamKey);
        await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.Beginning, createStream: true);
    }

    private async Task AddAsync(long roomId)
    {
        var db = fixture.RedisConnection.GetDatabase();
        await db.StreamAddAsync(StreamKey, "data", JsonSerializer.Serialize(new DungeonClearMessage
        {
            RoomId = roomId,
            MapId = MapIds.Dungeon01,
            Participants = [],
        }));
    }

    [Fact]
    public async Task 핸들러가_실패하면_재배달돼_다시_시도된다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await ResetStreamAsync();
        await AddAsync(95001);

        int attempts = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var queue = new FastSweepQueue(fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance, maxAttempts: 10);

        var run = ResilientStreamConsumer.RunAsync<DungeonClearMessage>(
            "redelivery-test",
            queue.DequeueAllAsync,
            (_, _) =>
            {
                // 첫 시도만 실패(일시적 인프라 오류 재현). 두 번째 시도는 성공해야 한다.
                if (Interlocked.Increment(ref attempts) == 1)
                    throw new InvalidOperationException("transient failure");
                return Task.CompletedTask;
            },
            NullLogger.Instance,
            cts.Token,
            baseDelay: TimeSpan.FromMilliseconds(10), maxDelay: TimeSpan.FromMilliseconds(10));

        while (!cts.IsCancellationRequested)
        {
            if (Volatile.Read(ref attempts) >= 2 && (await db.StreamPendingAsync(StreamKey, GroupName)).PendingMessageCount == 0)
                break;
            await Task.Delay(50, CancellationToken.None);
        }
        await cts.CancelAsync();
        await run;

        Assert.Equal(2, Volatile.Read(ref attempts));                                   // 실패 1회 + 재시도 성공 1회
        Assert.Equal(0, (await db.StreamPendingAsync(StreamKey, GroupName)).PendingMessageCount); // 성공 후 ACK
    }

    [Fact]
    public async Task 항상_실패하는_메시지는_재시도_상한에서_드롭된다()
    {
        var db = fixture.RedisConnection.GetDatabase();
        await ResetStreamAsync();
        await AddAsync(95002);

        int attempts = 0;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var queue = new FastSweepQueue(fixture.RedisConnection, NullLogger<DungeonClearMessageQueue>.Instance, maxAttempts: 3);

        var run = ResilientStreamConsumer.RunAsync<DungeonClearMessage>(
            "poison-test",
            queue.DequeueAllAsync,
            (_, _) =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("always fails");
            },
            NullLogger.Instance,
            cts.Token,
            baseDelay: TimeSpan.FromMilliseconds(10), maxDelay: TimeSpan.FromMilliseconds(10));

        while (!cts.IsCancellationRequested)
        {
            if ((await db.StreamPendingAsync(StreamKey, GroupName)).PendingMessageCount == 0 && Volatile.Read(ref attempts) > 0)
                break;
            await Task.Delay(50, CancellationToken.None);
        }
        await Task.Delay(500, CancellationToken.None); // 드롭 후 더 시도하지 않는지 확인할 여유
        await cts.CancelAsync();
        await run;

        // 상한(3)만큼만 시도하고 드롭 — 아니면 30초마다 영원히 같은 독을 다시 집는다.
        Assert.Equal(3, Volatile.Read(ref attempts));
        Assert.Equal(0, (await db.StreamPendingAsync(StreamKey, GroupName)).PendingMessageCount);
    }
}
