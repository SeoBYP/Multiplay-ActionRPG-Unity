using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// 게임 시작 요청 큐. GameServer 가 Outbox 로 발행하고, 같은 스트림을
/// SocketServer("socket-server" 그룹)와 GameServer("game-session-service" 그룹)가 각자 소비한다.
/// </summary>
public class GameStartRequestedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameStartRequestedMessageQueue> logger)
    : RedisMessageQueueBase<GameStartRequestedMessage>(redis, "stream:game:start:requested")
{
    private const string GroupName = "game-session-service";
    private static readonly string ConsumerName = StableConsumerName("gameserver");

    public override async Task EnqueueAsync(GameStartRequestedMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation(
            "Enqueued game start requested message for room {RoomId} with {PlayerCount} players",
            message.RoomId, message.PlayerInfos.Count);
    }

    public override IAsyncEnumerable<StreamMessage<GameStartRequestedMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
