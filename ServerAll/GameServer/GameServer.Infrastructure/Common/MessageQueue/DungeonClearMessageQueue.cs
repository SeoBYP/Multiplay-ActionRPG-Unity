using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// SocketServer가 발행한 던전 클리어 이벤트를 소비하는 Consumer Group 큐.
/// stream:game:dungeon:result 스트림을 구독해 GameServer에서 보상을 산정/지급한다(B 트랙).
/// 소비 루프·PEL 회수는 RedisMessageQueueBase 공통.
/// </summary>
public class DungeonClearMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<DungeonClearMessageQueue> logger)
    : RedisMessageQueueBase<DungeonClearMessage>(redis, "stream:game:dungeon:result")
{
    private const string GroupName = "dungeon-result-service";
    private static readonly string ConsumerName = StableConsumerName("gameserver");

    public override Task EnqueueAsync(DungeonClearMessage message)
        => throw new NotSupportedException("GameServer는 소비만 한다.");

    public override IAsyncEnumerable<StreamMessage<DungeonClearMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
