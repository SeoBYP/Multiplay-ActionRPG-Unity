using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class GameStartRequestedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameStartRequestedMessageQueue> logger)
    : RedisMessageQueueBase<GameStartRequestedMessage>(redis, "stream:game:start:requested")
{
    private const string GroupName = "socket-server";
    private static readonly string ConsumerName = StableConsumerName("socket");

    public override Task EnqueueAsync(GameStartRequestedMessage message)
        => throw new NotSupportedException();

    public override IAsyncEnumerable<StreamMessage<GameStartRequestedMessage>> DequeueAllAsync(CancellationToken ct = default)
        => ConsumeGroupAsync(GroupName, ConsumerName, logger, ct);
}
