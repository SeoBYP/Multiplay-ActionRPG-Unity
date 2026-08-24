using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Server;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Server.Tests.MessageQueue;

/// <summary>
/// SocketServer 가 게임 시작 요청을 소비하는 Consumer Group 의 **PEL 자동 회수** 검증 (F4).
///
/// 이 큐가 막히면 던전 입장 자체가 안 된다 — ACK 전에 SocketServer 가 죽으면 그 요청은
/// 그 컨슈머의 PEL 에 남고, 회수 주체가 없으면 방이 영원히 Starting 에 머문다.
/// 회수 로직은 Shared 베이스에 있지만 **SocketServer 그룹으로도 실제로 도는지**를 여기서 고정한다
/// (연결 처리 커버리지 정책 — .claude/rules/testing.md).
/// </summary>
public class GameStartRequestedPelReclaimTests : IAsyncLifetime
{
    private const string StreamKey = "stream:game:start:requested";
    private const string GroupName = "socket-server";

    private RedisContainer _redis = null!;
    private IConnectionMultiplexer _connection = null!;

    public async Task InitializeAsync()
    {
        _redis = new RedisBuilder().Build();
        await _redis.StartAsync();
        _connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _redis.DisposeAsync();
    }

    /// <summary>회수 파라미터만 압축한 실제 큐 — 로직은 프로덕션 그대로.</summary>
    private sealed class FastSweepQueue(IConnectionMultiplexer redis, ILogger<GameStartRequestedMessageQueue> logger)
        : GameStartRequestedMessageQueue(redis, logger)
    {
        protected override TimeSpan PendingMinIdle => TimeSpan.FromMilliseconds(200);
        protected override TimeSpan AutoClaimInterval => TimeSpan.FromMilliseconds(100);
        protected override TimeSpan IdlePollDelay => TimeSpan.FromMilliseconds(50);
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
    public async Task ACK_전에_죽은_SocketServer_의_게임시작요청을_회수해_다시_처리한다()
    {
        var db = _connection.GetDatabase();
        await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, StreamPosition.Beginning, createStream: true);

        var payload = JsonSerializer.Serialize(new GameStartRequestedMessage
        {
            RoomId = 91001,
            PlayerInfos = [new PlayerInfo { UserId = 9101, Nickname = "reclaim-tester" }],
        });
        await db.StreamAddAsync(StreamKey, "data", payload);

        // 죽은 SocketServer 재현 — 읽어가서 PEL 에 넣고 ACK 하지 않는다.
        var stolen = await db.StreamReadGroupAsync(StreamKey, GroupName, "socket-dead", ">", count: 10);
        Assert.Single(stolen);

        await Task.Delay(400); // PendingMinIdle 경과

        var queue = new FastSweepQueue(_connection, NullLogger<GameStartRequestedMessageQueue>.Instance);
        var received = await FirstOrTimeoutAsync(queue.DequeueAllAsync(CancellationToken.None), TimeSpan.FromSeconds(5));

        Assert.NotNull(received);
        Assert.Equal(91001, received.Payload.RoomId);

        // 회수는 소유권만 옮긴다 — ACK 는 아직이다(핸들러가 성공해야 ACK, at-least-once).
        var beforeAck = await db.StreamPendingAsync(StreamKey, GroupName);
        Assert.Equal(1, beforeAck.PendingMessageCount);

        // 소비자가 처리에 성공해 ACK 하면 그제서야 PEL 에서 빠진다.
        await received.AcknowledgeAsync();
        var afterAck = await db.StreamPendingAsync(StreamKey, GroupName);
        Assert.Equal(0, afterAck.PendingMessageCount);
    }
}
