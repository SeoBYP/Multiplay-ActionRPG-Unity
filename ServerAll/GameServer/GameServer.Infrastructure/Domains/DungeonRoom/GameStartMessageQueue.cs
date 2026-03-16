using System.Text.Json;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class GameStartMessageQueue(IConnectionMultiplexer redis)
    : RedisMessageQueueBase<GameStartMessage>(redis, "stream:game:start"), IGameStartPublisher
{
    private const string EntryId = "data";

    public Task PublishAsync(GameStartMessage message, CancellationToken ct = default)
        => EnqueueAsync(message);

    public override async Task EnqueueAsync(GameStartMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryId, json)]);
    }

    // GameServer는 소비 안 함
    public override IAsyncEnumerable<GameStartMessage> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException();

    protected override ValueTask<string> SerializeMessage(GameStartMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameStartMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameStartMessage>(data)!);
}