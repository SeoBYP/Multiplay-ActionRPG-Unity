using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Infrastructure.MessageQueue;

/// <summary>
/// Redis Stream 기반 메시지 큐의 공통 베이스.
///
/// 소비 측(Consumer Group) 루프는 7개 큐가 **같은 코드를 각자 복사**해 갖고 있었다(~120줄 × 7).
/// PEL 회수 같은 보강을 넣으려면 7곳을 똑같이 고쳐야 했으므로 루프를 여기 한 벌로 모았다.
/// 파생 큐는 스트림 키·그룹 이름·직렬화만 제공하고 <see cref="ConsumeGroupAsync"/> 를 호출한다.
/// </summary>
public abstract class RedisMessageQueueBase<T> : IMessageQueue<T> where T : class
{
    /// <summary>스트림 엔트리의 페이로드 필드명 — 모든 큐 공통.</summary>
    protected const string EntryKey = "data";

    protected readonly IConnectionMultiplexer Redis;
    protected readonly IDatabase Database;
    protected readonly string QueueKey;

    protected RedisMessageQueueBase(IConnectionMultiplexer redis, string queueKey)
    {
        Redis = redis;
        Database = redis.GetDatabase();
        QueueKey = queueKey;
    }

    public abstract Task EnqueueAsync(T message);
    public abstract IAsyncEnumerable<StreamMessage<T>> DequeueAllAsync(CancellationToken cancellationToken = default);

    protected virtual ValueTask<string> SerializeMessage(T message)
        => ValueTask.FromResult(JsonSerializer.Serialize(message));

    protected virtual ValueTask<T> DeserializeMessage(string data)
        => ValueTask.FromResult(JsonSerializer.Deserialize<T>(data)!);

    /// <summary>새 메시지가 없을 때의 폴링 간격.</summary>
    protected virtual TimeSpan IdlePollDelay => TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// PEL 회수 대상으로 볼 최소 유휴 시간. 이보다 오래 ACK 되지 않은 메시지는 주인이 죽은 것으로 본다.
    /// 살아 있는 컨슈머가 처리 중인 메시지를 빼앗지 않을 만큼 넉넉해야 한다.
    /// </summary>
    protected virtual TimeSpan PendingMinIdle => TimeSpan.FromSeconds(60);

    /// <summary>PEL 회수 스윕 주기. 유휴 구간에서만 수행해 정상 처리량에 부하를 주지 않는다.</summary>
    protected virtual TimeSpan AutoClaimInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// 같은 메시지를 처리 시도할 최대 횟수. at-least-once 라 실패는 재배달되는데,
    /// **항상** 실패하는 메시지(잘못된 데이터 등)는 상한이 없으면 영원히 재시도된다.
    /// 상한을 넘으면 Error 로그와 함께 ACK 로 드롭한다.
    /// </summary>
    protected virtual int MaxDeliveryAttempts => 5;

    /// <summary>스트림에 1건 발행(XADD).</summary>
    protected async Task PublishAsync(T message)
    {
        var json = await SerializeMessage(message);
        await Database.StreamAddAsync(QueueKey, [new NameValueEntry(EntryKey, json)]);
    }

    /// <summary>
    /// Consumer Group 소비 루프.
    ///
    ///   EnsureGroup → 내 PEL("0") → { XREADGROUP ">" ─있음→ 처리
    ///                                                └없음→ 스윕 주기 도달 시 XAUTOCLAIM } 반복
    ///
    /// ACK 는 하지 않고 봉투(<see cref="StreamMessage{T}"/>)로 넘긴다 — 핸들러가 성공해야 ACK 한다(at-least-once).
    /// </summary>
    protected async IAsyncEnumerable<StreamMessage<T>> ConsumeGroupAsync(
        string groupName,
        string consumerName,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await EnsureConsumerGroupAsync(groupName);

        // 같은 이름으로 재기동했다면 이전 생에서 ACK 하지 못한 내 몫이 남아 있다.
        await foreach (var pending in ReadOwnPendingAsync(groupName, consumerName, logger, ct))
            yield return pending;

        var nextSweepAt = DateTime.UtcNow + AutoClaimInterval;

        while (!ct.IsCancellationRequested)
        {
            StreamEntry[] entries;
            try
            {
                entries = await Database.StreamReadGroupAsync(QueueKey, groupName, consumerName, ">", count: 10);
            }
            catch (RedisException ex) when (IsMissingGroup(ex))
            {
                logger.LogWarning("Consumer group missing for {QueueKey}. Recreating.", QueueKey);
                await EnsureConsumerGroupAsync(groupName);
                continue;
            }

            if (entries.Length > 0)
            {
                foreach (var entry in entries)
                {
                    var message = await ProcessEntryAsync(entry, groupName, logger);
                    if (message is not null)
                        yield return message;
                }
                continue;
            }

            // 유휴 구간에서만 회수한다 — 처리량이 있을 때 스윕을 끼워 넣으면 Redis 부하만 늘린다.
            if (DateTime.UtcNow >= nextSweepAt)
            {
                nextSweepAt = DateTime.UtcNow + AutoClaimInterval;
                await foreach (var reclaimed in ReclaimStalePendingAsync(groupName, consumerName, logger, ct))
                    yield return reclaimed;
            }

            await Task.Delay(IdlePollDelay, ct);
        }
    }

    /// <summary>
    /// 죽은 컨슈머의 PEL 회수(XAUTOCLAIM).
    ///
    /// ACK 전에 프로세스가 죽으면 그 엔트리는 그 컨슈머의 PEL 에 남고, 회수 주체가 없으면 영구 잔류한다.
    /// <see cref="PendingMinIdle"/> 보다 오래 유휴인 것만 집으므로 살아 있는 컨슈머가 처리 중인 것은 빼앗지 않는다.
    /// </summary>
    private async IAsyncEnumerable<StreamMessage<T>> ReclaimStalePendingAsync(
        string groupName,
        string consumerName,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken ct)
    {
        RedisValue cursor = "0-0";
        int rounds = 0;
        int total = 0;

        // 한 스윕에서 무한히 돌지 않도록 상한을 둔다(대량 잔류 시 다음 스윕이 이어받는다).
        while (!ct.IsCancellationRequested && rounds++ < 10)
        {
            StreamAutoClaimResult? result = null;
            try
            {
                result = await Database.StreamAutoClaimAsync(
                    QueueKey, groupName, consumerName,
                    minIdleTimeInMs: (long)PendingMinIdle.TotalMilliseconds,
                    startAtId: cursor,
                    count: 10);
            }
            catch (RedisException ex)
            {
                logger.LogWarning(ex, "XAUTOCLAIM failed for {QueueKey}/{Group}", QueueKey, groupName);
            }

            if (result is null) break;

            var attempts = await GetDeliveryCountsAsync(groupName, consumerName);
            foreach (var entry in result.Value.ClaimedEntries)
            {
                total++;
                if (await DropIfExhaustedAsync(entry, attempts, groupName, logger)) continue;

                var message = await ProcessEntryAsync(entry, groupName, logger);
                if (message is not null)
                    yield return message;
            }

            cursor = result.Value.NextStartId;
            if (cursor.IsNullOrEmpty || cursor == "0-0") break;
        }

        if (total > 0)
            logger.LogInformation("Reclaimed {Count} stale pending entries from {QueueKey}/{Group}", total, QueueKey, groupName);
    }

    private async IAsyncEnumerable<StreamMessage<T>> ReadOwnPendingAsync(
        string groupName,
        string consumerName,
        ILogger logger,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var entries = await Database.StreamReadGroupAsync(QueueKey, groupName, consumerName, "0", count: 10);
        var attempts = await GetDeliveryCountsAsync(groupName, consumerName);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (await DropIfExhaustedAsync(entry, attempts, groupName, logger)) continue;

            var message = await ProcessEntryAsync(entry, groupName, logger);
            if (message is not null)
                yield return message;
        }
    }

    /// <summary>
    /// 엔트리 1건 → 봉투. ACK 는 하지 않고 <see cref="StreamMessage{T}"/> 에 담아 넘긴다
    /// (핸들러가 성공해야 ACK — at-least-once).
    ///
    /// 예외는 역직렬화 실패다. 다시 배달해도 같은 결과이므로 **즉시 ACK 로 PEL 에서 치운다** —
    /// 남겨두면 회수 로직이 매 스윕마다 같은 독을 다시 집는다.
    /// </summary>
    private async Task<StreamMessage<T>?> ProcessEntryAsync(StreamEntry entry, string groupName, ILogger logger)
    {
        T? payload = null;
        try
        {
            payload = await DeserializeMessage(entry[EntryKey].ToString());
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to deserialize entry {EntryId} from {QueueKey}", entry.Id, QueueKey);
        }

        if (payload is null)
        {
            await Database.StreamAcknowledgeAsync(QueueKey, groupName, entry.Id);
            return null;
        }

        var entryId = entry.Id;
        return new StreamMessage<T>(payload, () => Database.StreamAcknowledgeAsync(QueueKey, groupName, entryId));
    }

    /// <summary>
    /// 내 PEL 의 엔트리별 배달 횟수(XPENDING). 재배달 경로에서만 필요하다 —
    /// XREADGROUP ">" 로 처음 온 메시지는 항상 1회차다.
    /// </summary>
    private async Task<Dictionary<string, int>> GetDeliveryCountsAsync(string groupName, string consumerName)
    {
        try
        {
            var pending = await Database.StreamPendingMessagesAsync(QueueKey, groupName, count: 64, consumerName);
            var map = new Dictionary<string, int>(pending.Length);
            foreach (var info in pending)
                map[info.MessageId.ToString()] = info.DeliveryCount;
            return map;
        }
        catch (RedisException)
        {
            // 배달 횟수를 못 읽으면 드롭 판단을 하지 않는다(보수적 — 유실보다 재시도가 낫다).
            return [];
        }
    }

    /// <summary>
    /// 재시도 상한을 넘었으면 ACK 로 드롭하고 true. at-least-once 는 실패를 재배달하므로,
    /// **항상** 실패하는 메시지에 상한이 없으면 스윕마다 같은 독을 영원히 다시 집는다.
    /// </summary>
    private async Task<bool> DropIfExhaustedAsync(
        StreamEntry entry, Dictionary<string, int> attempts, string groupName, ILogger logger)
    {
        if (!attempts.TryGetValue(entry.Id.ToString(), out var delivered) || delivered <= MaxDeliveryAttempts)
            return false;

        logger.LogError(
            "Dropping entry {EntryId} from {QueueKey}/{Group} after {Delivered} delivery attempts (limit {Limit})",
            entry.Id, QueueKey, groupName, delivered, MaxDeliveryAttempts);
        await Database.StreamAcknowledgeAsync(QueueKey, groupName, entry.Id);
        return true;
    }

    private async Task EnsureConsumerGroupAsync(string groupName)
    {
        try
        {
            await Database.StreamCreateConsumerGroupAsync(
                QueueKey, groupName, StreamPosition.Beginning, createStream: true);
        }
        catch (RedisException)
        {
            // 이미 존재하면 무시(BUSYGROUP)
        }
    }

    private static bool IsMissingGroup(RedisException ex)
        => ex.Message.Contains("NOGROUP") || ex.Message.Contains("no such key");

    /// <summary>
    /// 인스턴스 고유 consumer 이름. 컨테이너 MachineName(=hostname)은 재시작해도 안정적이라
    /// 자기 PEL 복구(<see cref="ReadOwnPendingAsync"/>)가 실제로 동작한다.
    /// 매 기동 GUID 를 붙이면 재시작 시 "빈 새 PEL" 을 읽게 돼 이전 생의 미ACK 메시지를 영영 못 본다.
    /// </summary>
    protected static string StableConsumerName(string prefix)
        => $"{prefix}-{Environment.MachineName}";
}
