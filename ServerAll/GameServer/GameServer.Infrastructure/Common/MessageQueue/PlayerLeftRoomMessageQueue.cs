using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common.MessageQueue;

/// <summary>
/// SocketServer가 발행한 PlayerLeft 이벤트를 소비하는 Consumer Group 큐.
/// stream:game:room:lifecycle 스트림을 구독해 GameServer에서 플레이어 association을 정리한다.
/// </summary>
public class PlayerLeftRoomMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<PlayerLeftRoomMessageQueue> logger)
    : RedisMessageQueueBase<PlayerLeftRoomMessage>(redis, "stream:game:room:lifecycle"),
        IMessageQueue<PlayerLeftRoomMessage>
{
    private const string EntryKey    = "data";
    private const string GroupName   = "room-lifecycle-service";
    private readonly string _consumerName = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public override Task EnqueueAsync(PlayerLeftRoomMessage message)
        => throw new NotSupportedException("GameServer는 소비만 한다.");

    public override async IAsyncEnumerable<PlayerLeftRoomMessage> DequeueAllAsync(
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

    private async IAsyncEnumerable<PlayerLeftRoomMessage> ReadPendingAsync(
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

    private async Task<PlayerLeftRoomMessage?> ProcessEntryAsync(StreamEntry entry)
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
            logger.LogError(e, "Failed to process PlayerLeft entry {EntryId}", entry.Id);
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

    protected override ValueTask<string> SerializeMessage(PlayerLeftRoomMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<PlayerLeftRoomMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<PlayerLeftRoomMessage>(data)!);
}
