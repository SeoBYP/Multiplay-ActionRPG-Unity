using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class GameStartMessageQueue(
    IConnectionMultiplexer redis,
    ILogger<GameStartMessageQueue> logger)
    : RedisMessageQueueBase<GameStartMessage>(redis, "stream:game:start")
{
    private const string EntryId    = "data";
    private const string GroupName  = "socket-server";
    private const string ConsumerName = "socket-1";
    
    // SocketServer는 발행 안 함
    public override Task EnqueueAsync(GameStartMessage message)
        => throw new NotSupportedException();

    public override async IAsyncEnumerable<GameStartMessage> DequeueAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureConsumerGroupAsync();

        // 1. 재시작 시 PEL(Pending Entry List)에 남은 내 메시지 먼저 처리
        // ConsumerName이 고정되어 있다면, 이전에 가져갔다가 ACK 안 한 메시지들이 있음.
        await foreach (var msg in ReadPendingAsync(ct))
            yield return msg;

        // 2. 새 메시지 읽기 루프
        while (!ct.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                // ">" : 다른 소비자에게 전달되지 않은 새 메시지만 읽음
                entries = await Database.StreamReadGroupAsync(QueueKey, GroupName, ConsumerName, ">", count: 10);
            }
            catch (RedisException ex) when (ex.Message.Contains("NOGROUP") || ex.Message.Contains("no such key"))
            {
                logger.LogWarning("Stream or Consumer Group missing. Recreating...");
                await EnsureConsumerGroupAsync();
                continue;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error reading from Redis Stream");
                throw;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var message = await ProcessEntryAsync(entry);
                if (message != null)
                    yield return message;
            }
        }
    }

    private async IAsyncEnumerable<GameStartMessage> ReadPendingAsync([EnumeratorCancellation] CancellationToken ct)
    {
        // "0" 또는 특정 ID를 주면 Pending 메시지를 읽음
        // 여기서는 간단히 한 번만 시도 (필요시 루프로 구현)
        StreamEntry[] entries = await Database.StreamReadGroupAsync(QueueKey, GroupName, ConsumerName, "0", count: 10);
        foreach (var entry in entries)
        {
            logger.LogInformation("Processing pending message: {Id}", entry.Id);
            var message = await ProcessEntryAsync(entry);
            if (message != null)
                yield return message;
        }
    }

    private async Task<GameStartMessage?> ProcessEntryAsync(StreamEntry entry)
    {
        try
        {
            var json = entry[EntryId].ToString();
            var message = await DeserializeMessage(json);
            await Database.StreamAcknowledgeAsync(QueueKey, GroupName, entry.Id);
            return message;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error processing StreamEntry: {Id}", entry.Id);
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
            /* 이미 존재 — 무시 */
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error ensuring Consumer Group");
            throw;
        }
    } 
    protected override ValueTask<string> SerializeMessage(GameStartMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameStartMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameStartMessage>(data)!);
}