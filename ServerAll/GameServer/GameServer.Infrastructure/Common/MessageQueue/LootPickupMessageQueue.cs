using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// SocketServer가 발행한 줍기 확정 이벤트를 소비하는 Consumer Group 큐.
/// stream:game:loot:pickup 스트림을 구독해 GameServer에서 인벤토리에 영속 지급한다(LootGrantConsumer).
/// </summary>
public class LootPickupMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<LootPickupMessageQueue> logger)
    : RedisMessageQueueBase<ItemPickedUpMessage>(redis, "stream:game:loot:pickup")
{
    private const string GroupName = "loot-grant-service";
    private static readonly string ConsumerName = StableConsumerName("gameserver");

    public override Task EnqueueAsync(ItemPickedUpMessage message)
        => throw new NotSupportedException("GameServer는 소비만 한다.");

    public override IAsyncEnumerable<StreamMessage<ItemPickedUpMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
