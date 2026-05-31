using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

/// <summary>
/// 방 생명주기 이벤트를 GameServer에 발행하는 큐 (SocketServer 전용 Publisher).
/// stream:game:room:lifecycle 스트림에 PlayerLeftRoomMessage 를 기록한다.
/// </summary>
public class RoomLifecycleMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<RoomLifecycleMessageQueue> logger)
    : RedisMessageQueueBase<PlayerLeftRoomMessage>(redis, "stream:game:room:lifecycle"), IRoomLifecyclePublisher
{
    private const string EntryKey = "data";

    public override async Task EnqueueAsync(PlayerLeftRoomMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
        logger.LogInformation(
            "[RoomLifecycle] Published PlayerLeft: RoomId={RoomId} UserId={UserId} RoomEmptied={RoomEmptied}",
            message.RoomId, message.UserId, message.RoomEmptied);
    }

    public override IAsyncEnumerable<PlayerLeftRoomMessage> DequeueAllAsync(CancellationToken ct = default)
        => throw new NotSupportedException("SocketServer는 발행만 한다.");

    protected override ValueTask<string> SerializeMessage(PlayerLeftRoomMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<PlayerLeftRoomMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<PlayerLeftRoomMessage>(data)!);
}
