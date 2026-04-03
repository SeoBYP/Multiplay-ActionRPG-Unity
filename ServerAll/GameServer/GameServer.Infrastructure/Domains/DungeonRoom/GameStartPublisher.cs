using System.Text.Json;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class GameStartPublisher(
    IConnectionMultiplexer redis,
    ILogger<GameStartPublisher> logger)
    : RedisMessageQueueBase<GameStartMessage>(redis, "stream:game:start"), IGameStartPublisher
{
    private const string EntryKey = "data";

    public async Task PublishGameStartAsync(GameStartRequestedMessage message, CancellationToken ct = default)
    {
        await EnqueueAsync(new GameStartMessage
        {
            RoomId = message.RoomId,
            PlayerIds = [.. message.PlayerIds],
            TraceId = message.TraceId
        });

        logger.LogInformation("Published game start for room {RoomId}", message.RoomId);
    }

    public override async Task EnqueueAsync(GameStartMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
    }

    public override IAsyncEnumerable<GameStartMessage> DequeueAllAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    protected override ValueTask<string> SerializeMessage(GameStartMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameStartMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameStartMessage>(data)!);
}
