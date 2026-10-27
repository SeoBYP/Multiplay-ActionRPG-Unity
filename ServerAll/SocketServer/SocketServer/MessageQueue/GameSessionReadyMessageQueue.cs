using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

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
