using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class GameSessionReadyMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameSessionReadyMessageQueue> logger)
    : RedisMessageQueueBase<GameSessionReadyMessage>(redis, "stream:game:session:ready")
{
    private const string EntryKey = "data";

    public override async Task EnqueueAsync(GameSessionReadyMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
        logger.LogInformation("Published game session ready for room {RoomId}", message.RoomId);
    }

    public override IAsyncEnumerable<GameSessionReadyMessage> DequeueAllAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    protected override ValueTask<string> SerializeMessage(GameSessionReadyMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameSessionReadyMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameSessionReadyMessage>(data)!);
}
