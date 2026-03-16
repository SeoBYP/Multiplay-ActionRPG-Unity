using System.Runtime.CompilerServices;
using System.Text.Json;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace Server;

public class GameStartMessageQueue(IConnectionMultiplexer redis)
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

        while (!ct.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await Database.StreamReadGroupAsync(QueueKey, GroupName, ConsumerName, ">", count: 10);
            }
            catch (RedisException ex) when (ex.Message.Contains("NOGROUP") || ex.Message.Contains("no such key"))
            {
                Console.WriteLine("RedisException: {0}", ex.Message);
                await EnsureConsumerGroupAsync();  // 스트림/그룹 재생성
                continue;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(100, ct);
                continue;
            }

            foreach (var entry in entries)
            {
                var json = entry[EntryId].ToString();
                var message = await DeserializeMessage(json);
                await Database.StreamAcknowledgeAsync(QueueKey, GroupName, entry.Id);
                yield return message;
            }
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await Database.StreamCreateConsumerGroupAsync(
                QueueKey, GroupName, 
                StreamPosition.Beginning,  // "0" — 재시작 시 미처리 메시지도 다시 읽음
                createStream: true);
        }
        catch (RedisException)
        {
            /* 이미 존재 — 무시 */
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    } 
    protected override ValueTask<string> SerializeMessage(GameStartMessage message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected override ValueTask<GameStartMessage> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<GameStartMessage>(data)!);
}