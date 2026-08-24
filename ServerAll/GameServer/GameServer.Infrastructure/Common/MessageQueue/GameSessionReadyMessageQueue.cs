using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// 게임 세션 준비 완료 이벤트 큐. GameServer 는 발행·소비 양쪽을 한다
/// (SocketServer 가 세션을 띄운 뒤 발행하고, DungeonLobby 스트림으로 흘리기 위해 자기 그룹으로 되읽는다).
/// </summary>
public class GameSessionReadyMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameSessionReadyMessageQueue> logger)
    : RedisMessageQueueBase<GameSessionReadyMessage>(redis, "stream:game:session:ready")
{
    private const string GroupName = "dungeon-lobby-service";
    private static readonly string ConsumerName = StableConsumerName("gameserver");

    public override async Task EnqueueAsync(GameSessionReadyMessage message)
    {
        await PublishAsync(message);
        logger.LogInformation(
            "Enqueued game session ready message for room {RoomId}, session {GameSessionId}",
            message.RoomId, message.GameSessionId);
    }

    public override IAsyncEnumerable<StreamMessage<GameSessionReadyMessage>> DequeueAllAsync(CancellationToken cancellationToken = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, cancellationToken);
}
