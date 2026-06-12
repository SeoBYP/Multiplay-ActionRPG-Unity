using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Shared.Infrastructure.MessageQueue;

/// <summary>
/// GameServer(발행) → SocketServer(소비) 단일 큐. 소모품 소비 통지(서버 권위 회복).
/// 큐 클래스를 Shared 에 두어 양쪽이 같은 타입을 공유(게임시작 큐의 양측 복제 대신 단일화).
/// Consumer Group("socket-consume") — 재시작 시 미처리 메시지 재처리(StreamPosition.Beginning).
/// </summary>
public class PlayerConsumedMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<PlayerConsumedMessageQueue> logger)
    : RedisMessageQueueBase<PlayerConsumedMessage>(redis, "stream:game:player:consumed"),
        IMessageQueue<PlayerConsumedMessage>
{
    private const string EntryKey = "data";
    private const string GroupName = "socket-consume";
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public override async Task EnqueueAsync(PlayerConsumedMessage message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
        logger.LogInformation("Enqueued player consumed: User {UserId} Effect {EffectId}", message.UserId, message.EffectId);
    }

    public override async IAsyncEnumerable<PlayerConsumedMessage> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureConsumerGroupAsync();

        await foreach (var pending in ReadPendingAsync(cancellationToken))
            yield return pending;

        while (!cancellationToken.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await Database.StreamReadGroupAsync(QueueKey, GroupName, _consumerName, ">", count: 10);
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

    private async IAsyncEnumerable<PlayerConsumedMessage> ReadPendingAsync(
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

    private async Task<PlayerConsumedMessage?> ProcessEntryAsync(StreamEntry entry)
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
            logger.LogError(e, "Failed to process player consumed entry {EntryId}", entry.Id);
            return null;
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await Database.StreamCreateConsumerGroupAsync(QueueKey, GroupName, StreamPosition.Beginning, createStream: true);
        }
        catch (RedisException)
        {
        }
    }

    protected override ValueTask<string> SerializeMessage(PlayerConsumedMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<PlayerConsumedMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<PlayerConsumedMessage>(data)!);
}
