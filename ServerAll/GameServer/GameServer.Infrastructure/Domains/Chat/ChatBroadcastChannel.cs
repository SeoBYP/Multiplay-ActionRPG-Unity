using System.Text.Json;
using GameServer.Domain.Entities.Chat;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.Chat;

public class ChatBroadcastChannel(
    IConnectionMultiplexer redis,
    ILogger<ChatBroadcastChannel> logger) : RedisBroadcastChannelBase<ChatMessage>(redis)
{
    private const string EntryId = "game:chat:msg";

    public override async Task PublishAsync(string channel, ChatMessage message, CancellationToken ct = default)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(channel, [new NameValueEntry(EntryId, json)]);
    }

    public override async IAsyncEnumerable<(string messageId, ChatMessage message)> ReadAsync(
        string channel,
        string lastMessageId,
        CancellationToken ct = default)
    {
        var currentId = lastMessageId;

        while (!ct.IsCancellationRequested)
        {
            var entries = await Database.StreamReadAsync(channel, currentId, count: 10);
            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                currentId = entry.Id;
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
            var message = JsonSerializer.Deserialize<ChatMessage>(data)!;
            return ValueTask.FromResult(message);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to deserialize chat broadcast message");
            throw;
        }
    }
}
