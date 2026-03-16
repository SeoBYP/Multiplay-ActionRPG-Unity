using Shared.Infrastructure.MessageQueue;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class DungeonRoomBroadcastChannel(IConnectionMultiplexer redis)
    : RedisBroadcastChannelBase<long>(redis)
{
    private const string EntryId = "game:dungeon:room:msg";
    public override async Task PublishAsync(string channel, long message, CancellationToken ct = default)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(channel, [new NameValueEntry(EntryId, json)]);
    }

    public override async IAsyncEnumerable<(string messageId, long message)> ReadAsync(string channel, 
        string lastMessageId, CancellationToken ct = default)
    {
        var currentId = lastMessageId;  // 파라미터를 로컬 복사

        while (!ct.IsCancellationRequested) 
        {
            // XREAD
            var entries = await Database.StreamReadAsync(channel, currentId, count: 100);
            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);  // 폴링 or XREAD BLOCK으로 대기
                continue;
            }
            foreach (var entry in entries)
            {
                currentId = entry.Id;
                var json = entry[EntryId].ToString();
                var roomId = await DeserializeMessage(json);
                yield return (currentId, roomId);
            }
        }
    }

    protected override ValueTask<string> SerializeMessage(long message)
    {
        return ValueTask.FromResult(message.ToString());
    }

    protected override ValueTask<long> DeserializeMessage(string json)
    {
        return ValueTask.FromResult(long.Parse(json));
    }
}