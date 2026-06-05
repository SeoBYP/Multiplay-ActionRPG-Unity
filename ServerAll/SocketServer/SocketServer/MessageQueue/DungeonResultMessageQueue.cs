using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

/// <summary>
/// 던전 결과(클리어) 이벤트를 GameServer에 발행하는 큐 (SocketServer 전용 Publisher).
/// stream:game:dungeon:result 스트림에 DungeonClearMessage 를 기록한다.
/// </summary>
public class DungeonResultMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<DungeonResultMessageQueue> logger)
    : RedisMessageQueueBase<DungeonClearMessage>(redis, "stream:game:dungeon:result"), IDungeonResultPublisher
{
    private const string EntryKey = "data";

    public override async Task EnqueueAsync(DungeonClearMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
        logger.LogInformation(
            "[DungeonResult] Published Clear: RoomId={RoomId} MapId={MapId} Participants={Count}",
            message.RoomId, message.MapId, message.Participants.Length);
    }

    public override IAsyncEnumerable<DungeonClearMessage> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");

    protected override ValueTask<string> SerializeMessage(DungeonClearMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<DungeonClearMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<DungeonClearMessage>(data)!);
}
