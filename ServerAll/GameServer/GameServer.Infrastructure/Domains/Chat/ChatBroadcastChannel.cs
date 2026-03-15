using System.Text.Json;
using GameServer.Domain.Entities.Chat;
using GameServer.Infrastructure.MessageQueue;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatBroadcastChannel(IConnectionMultiplexer redis) : RedisBroadcastChannelBase<ChatMessage>(redis)
{
    private const string EntryId = "game:chat:msg";
    
    public override async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct = default)
    {
        var json = await SerializeMessage(message);
        // XADD
        await Database.StreamAddAsync(channel, [new NameValueEntry(EntryId, json)]);
    }

    public override async IAsyncEnumerable<(string messageId, ChatMessage message)> ReadAsync(string channel,
        string lastMessageId, CancellationToken ct = default)
    {
        var currentId = lastMessageId;  // 파라미터를 로컬 복사

        while (!ct.IsCancellationRequested) 
        {
            // XREAD
            var entries = await Database.StreamReadAsync(channel, currentId, count: 10);
            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);  // 폴링 or XREAD BLOCK으로 대기
                continue;
            }

            foreach (var entry in entries)
            {
                currentId = entry.Id;  // 로컬 변수 업데이트
                var json = entry[EntryId].ToString();
                var msg = await DeserializeMessage(json);
                yield return (currentId, msg);
            }
        }
    }

    protected override ValueTask<string> SerializeMessage(ChatMessage message)
    {
        return ValueTask.FromResult(JsonSerializer.Serialize(message));
    }

    protected override ValueTask<ChatMessage> DeserializeMessage(string data)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ChatMessage>(data);
            return ValueTask.FromResult(message);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}