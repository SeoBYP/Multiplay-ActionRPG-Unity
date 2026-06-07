using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// SocketServer가 발행한 줍기 확정 이벤트를 소비하는 Consumer Group 큐.
/// stream:game:loot:pickup 스트림을 구독해 GameServer에서 인벤토리에 영속 지급한다(LootGrantConsumer).
/// DungeonClearMessageQueue 와 동일 구조(Consumer Group + pending 재처리).
/// </summary>
public class LootPickupMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<LootPickupMessageQueue> logger)
    : RedisMessageQueueBase<ItemPickedUpMessage>(redis, "stream:game:loot:pickup"),
        IMessageQueue<ItemPickedUpMessage>
{
    private const string EntryKey  = "data";
    private const string GroupName = "loot-grant-service";
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public override Task EnqueueAsync(ItemPickedUpMessage message)
        => throw new NotSupportedException("GameServer는 소비만 한다.");

    public override async IAsyncEnumerable<ItemPickedUpMessage> DequeueAllAsync(
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
                entries = await Database.StreamReadGroupAsync(
                    QueueKey, GroupName, _consumerName, ">", count: 10);
            }
            catch (RedisException ex) when (ex.Message.Contains("NOGROUP") || ex.Message.Contains("no such key"))
            {
                logger.LogWarning("Consumer group missing for {QueueKey}. Recreating.", QueueKey);
                await EnsureConsumerGroupAsync();
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(200, cancellationToken);
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

    private async IAsyncEnumerable<ItemPickedUpMessage> ReadPendingAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var entries = await Database.StreamReadGroupAsync(
            QueueKey, GroupName, _consumerName, "0", count: 10);

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var message = await ProcessEntryAsync(entry);
            if (message is not null)
                yield return message;
        }
    }

    private async Task<ItemPickedUpMessage?> ProcessEntryAsync(StreamEntry entry)
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
            logger.LogError(e, "Failed to process LootPickup entry {EntryId}", entry.Id);
            return null;
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await Database.StreamCreateConsumerGroupAsync(
                QueueKey, GroupName,
                StreamPosition.Beginning,
                createStream: true);
        }
        catch (RedisException)
        {
            // 이미 존재하면 무시
        }
    }

    protected override ValueTask<string> SerializeMessage(ItemPickedUpMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<ItemPickedUpMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<ItemPickedUpMessage>(data)!);
}
