using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class RoomLifecycleMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<RoomLifecycleMessageQueue> logger)
    : RedisMessageQueueBase<PlayerLeftRoomMessage>(redis, "stream:game:room:lifecycle"), IRoomLifecyclePublisher
{
    public override async Task EnqueueAsync(PlayerLeftRoomMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation(
            "[RoomLifecycle] Published PlayerLeft: RoomId={RoomId} UserId={UserId} RoomEmptied={RoomEmptied}",
            message.RoomId, message.UserId, message.RoomEmptied);
    }

    public override IAsyncEnumerable<StreamMessage<PlayerLeftRoomMessage>> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");
}
