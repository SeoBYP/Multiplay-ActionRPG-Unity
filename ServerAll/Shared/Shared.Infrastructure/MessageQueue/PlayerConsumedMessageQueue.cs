using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Shared.Infrastructure.MessageQueue;

/// <summary>
/// GameServer(발행) → SocketServer(소비) 단일 큐. 소모품 소비 통지(서버 권위 회복).
/// 큐 클래스를 Shared 에 두어 양쪽이 같은 타입을 공유(게임시작 큐의 양측 복제 대신 단일화).
/// </summary>
public class PlayerConsumedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<PlayerConsumedMessageQueue> logger)
    : RedisMessageQueueBase<PlayerConsumedMessage>(redis, "stream:game:player:consumed")
{
    private const string GroupName = "socket-consume";
    private static readonly string ConsumerName = StableConsumerName("socket");

    public override async Task EnqueueAsync(PlayerConsumedMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation("Enqueued player consumed: User {UserId} Effect {EffectId}", message.UserId, message.EffectId);
    }

    public override IAsyncEnumerable<StreamMessage<PlayerConsumedMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
