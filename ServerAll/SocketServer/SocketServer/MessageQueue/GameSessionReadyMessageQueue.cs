using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

/// <summary>
/// 게임 세션 준비 완료를 GameServer 에 발행하는 큐 (SocketServer 전용 Publisher).
/// </summary>
public class GameSessionReadyMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameSessionReadyMessageQueue> logger)
    : RedisMessageQueueBase<GameSessionReadyMessage>(redis, "stream:game:session:ready")
{
    public override async Task EnqueueAsync(GameSessionReadyMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation("Published game session ready for room {RoomId}", message.RoomId);
    }

    public override IAsyncEnumerable<StreamMessage<GameSessionReadyMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");
}
