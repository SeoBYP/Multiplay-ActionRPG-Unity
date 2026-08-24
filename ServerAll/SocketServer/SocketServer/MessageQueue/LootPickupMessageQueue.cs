using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

/// <summary>
/// 줍기 확정 이벤트를 GameServer 에 발행하는 큐 (SocketServer 전용 Publisher).
/// stream:game:loot:pickup 스트림에 ItemPickedUpMessage 를 기록한다.
/// GameServer LootGrantConsumer 가 소비해 인벤토리에 영속 지급(GrantItemAsync).
/// </summary>
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
