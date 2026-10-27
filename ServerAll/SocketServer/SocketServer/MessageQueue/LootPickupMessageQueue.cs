using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class LootPickupMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<LootPickupMessageQueue> logger)
    : RedisMessageQueueBase<ItemPickedUpMessage>(redis, "stream:game:loot:pickup"), ILootPickupPublisher
{
    public override async Task EnqueueAsync(ItemPickedUpMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation(
            "[LootPickup] Published Pickup: UserId={UserId} ItemId={ItemId} Qty={Qty} PickupId={PickupId}",
            message.UserId, message.ItemId, message.Qty, message.PickupId);
    }

    public override IAsyncEnumerable<StreamMessage<ItemPickedUpMessage>> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");
}
