using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// SocketServer가 발행한 PlayerLeft 이벤트를 소비하는 Consumer Group 큐.
/// stream:game:room:lifecycle 스트림을 구독해 GameServer에서 플레이어 association을 정리한다.
/// </summary>
public class PlayerLeftRoomMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<PlayerLeftRoomMessageQueue> logger)
    : RedisMessageQueueBase<PlayerLeftRoomMessage>(redis, "stream:game:room:lifecycle")
{
    private const string GroupName = "room-lifecycle-service";
    private static readonly string ConsumerName = StableConsumerName("gameserver");

    public override Task EnqueueAsync(PlayerLeftRoomMessage message)
        => throw new NotSupportedException("GameServer는 소비만 한다.");

    public override IAsyncEnumerable<StreamMessage<PlayerLeftRoomMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
