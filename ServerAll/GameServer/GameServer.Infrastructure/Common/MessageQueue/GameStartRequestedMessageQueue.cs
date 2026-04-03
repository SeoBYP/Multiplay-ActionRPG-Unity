using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

public class GameStartRequestedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameStartRequestedMessageQueue> logger)
    : RedisMessageQueueBase<GameStartRequestedMessage>(redis, "stream:game:start:requested"),
        IMessageQueue<GameStartRequestedMessage>
{
    private const string EntryKey = "data";
    private const string GroupName = "game-session-service";
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public override async Task EnqueueAsync(GameStartRequestedMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
        logger.LogInformation(
            "Enqueued game start requested message for room {RoomId} with {PlayerCount} players",
            message.RoomId,
            message.PlayerIds.Count);
    }

    public override async IAsyncEnumerable<GameStartRequestedMessage> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureConsumerGroupAsync();

        await foreach (var pendingMessage in ReadPendingAsync(cancellationToken))
            yield return pendingMessage;

        while (!cancellationToken.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await Database.StreamReadGroupAsync(
                    QueueKey,
                    GroupName,
                    _consumerName,
                    ">",
                    count: 10);
            }
            catch (RedisException ex) when (ex.Message.Contains("NOGROUP") || ex.Message.Contains("no such key"))
            {
                logger.LogWarning("Consumer group missing for queue {QueueKey}. Recreating.", QueueKey);
                await EnsureConsumerGroupAsync();
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(100, cancellationToken);
                continue;
            }

            foreach (var entry in entries)
            {
                var message = await ProcessEntryAsync(entry);
                if (message is not null)
                    yield return message;
            }
        }
    }

    private async IAsyncEnumerable<GameStartRequestedMessage> ReadPendingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var entries = await Database.StreamReadGroupAsync(QueueKey, GroupName, _consumerName, "0", count: 10);
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = await ProcessEntryAsync(entry);
            if (message is not null)
                yield return message;
        }
    }

    private async Task<GameStartRequestedMessage?> ProcessEntryAsync(StreamEntry entry)
    {
        try
        {
            var payload = entry[EntryKey].ToString();
            var message = await DeserializeMessage(payload);
            await Database.StreamAcknowledgeAsync(QueueKey, GroupName, entry.Id);
            return message;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to process game start requested entry {EntryId}", entry.Id);
            return null;
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await Database.StreamCreateConsumerGroupAsync(
                QueueKey,
                GroupName,
                StreamPosition.Beginning,
                createStream: true);
        }
        catch (RedisException)
        {
        }
    }

    protected override ValueTask<string> SerializeMessage(GameStartRequestedMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameStartRequestedMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameStartRequestedMessage>(data)!);
}
