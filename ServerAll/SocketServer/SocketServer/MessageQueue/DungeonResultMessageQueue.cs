using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class DungeonResultMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<DungeonResultMessageQueue> logger)
    : RedisMessageQueueBase<DungeonClearMessage>(redis, "stream:game:dungeon:result"), IDungeonResultPublisher
{
    public override async Task EnqueueAsync(DungeonClearMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation(
            "[DungeonResult] Published Clear: RoomId={RoomId} MapId={MapId} Participants={Count}",
            message.RoomId, message.MapId, message.Participants.Length);
    }

    public override IAsyncEnumerable<StreamMessage<DungeonClearMessage>> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");
}
