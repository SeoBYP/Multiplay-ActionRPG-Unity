using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

/// <summary>
/// GameServer 가 Outbox 로 발행한 게임 시작 요청을 소비한다(SocketServer 전용 그룹).
/// </summary>
public class GameStartRequestedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameStartRequestedMessageQueue> logger)
    : RedisMessageQueueBase<GameStartRequestedMessage>(redis, "stream:game:start:requested")
{
    private const string GroupName = "socket-server";
    private static readonly string ConsumerName = StableConsumerName("socket");

    // SocketServer는 발행 안 함
    public override Task EnqueueAsync(GameStartRequestedMessage message)
        => throw new NotSupportedException();

    public override IAsyncEnumerable<StreamMessage<GameStartRequestedMessage>> DequeueAllAsync(CancellationToken ct = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, ct);
}
