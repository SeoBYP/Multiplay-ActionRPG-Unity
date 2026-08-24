using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure.MessageQueue;

/// <summary>
/// BackgroundService 스트림 컨슈머의 공통 복원력 루프.
///
/// 외부 의존성(Redis 등)은 언제든 일시적으로 실패한다는 전제로, 스트림 읽기가 끊겨도
/// 컨슈머가 영구히 죽지 않도록 지수 백오프 후 재개한다(.NET BackgroundService 는 ExecuteAsync 가
/// 한 번 리턴하면 부활하지 않으므로 절대 빠져나가면 안 된다).
///
/// 3분류 처리:
///   - 취소(stoppingToken)                       → 정상 종료
///   - 스트림 읽기 실패(MoveNextAsync throw)        → 백오프 후 스트림 재개 (Redis LOADING/연결 끊김 등)
///   - 메시지 1건 핸들러 실패                      → ACK 하지 않아 재배달(at-least-once). 스트림은 유지
/// </summary>
public static class ResilientStreamConsumer
{
    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

    /// <param name="name">로깅용 컨슈머 이름.</param>
    /// <param name="readStream">큐의 스트림 읽기(예: queue.DequeueAllAsync). 읽기 실패는 재시도 대상.</param>
    /// <param name="handleMessage">
    /// 메시지 1건 처리. **성공해야 ACK 한다** — 던지면 ACK 하지 않아 재배달된다(at-least-once).
    /// 따라서 핸들러는 멱등해야 한다. 무한 재시도는 큐의 재시도 상한이 끊는다.
    /// </param>
    /// <param name="baseDelay">백오프 기준 지연(기본 1초). 테스트에서 단축용.</param>
    /// <param name="maxDelay">백오프 상한(기본 30초).</param>
    public static async Task RunAsync<TMessage>(
        string name,
        Func<CancellationToken, IAsyncEnumerable<StreamMessage<TMessage>>> readStream,
        Func<TMessage, CancellationToken, Task> handleMessage,
        ILogger logger,
        CancellationToken ct,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        var baseMs = (baseDelay ?? DefaultBaseDelay).TotalMilliseconds;
        var maxMs = (maxDelay ?? DefaultMaxDelay).TotalMilliseconds;

        logger.LogInformation("[{Name}] consumer started", name);
        int attempt = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var envelope in readStream(ct).WithCancellation(ct))
                {
                    attempt = 0; // 스트림이 정상 동작 중 → 백오프 카운터 리셋
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        await handleMessage(envelope.Payload, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return; // 정상 종료 — ACK 하지 않는다(처리 완료가 아니다)
                    }
                    catch (Exception e)
                    {
                        // 실패 → ACK 하지 않는다. 메시지는 PEL 에 남아 재배달된다(at-least-once).
                        // 스트림 자체는 유지한다(이 메시지 하나 때문에 컨슈머가 멈추면 안 된다).
                        logger.LogError(e, "[{Name}] message handler failed; leaving unacked for redelivery", name);
                        continue;
                    }

                    try
                    {
                        await envelope.AcknowledgeAsync();
                    }
                    catch (Exception e)
                    {
                        // ACK 실패는 재배달로 이어질 뿐이다(핸들러 멱등 전제). 컨슈머를 죽이지 않는다.
                        logger.LogWarning(e, "[{Name}] ack failed; message will be redelivered", name);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break; // 정상 종료
            }
            catch (Exception e)
            {
                // 스트림 읽기 자체가 끊김(일시적 인프라 오류) — 죽지 않고 백오프 후 재개.
                attempt++;
                var delay = Backoff(attempt, baseMs, maxMs);
                logger.LogWarning(e,
                    "[{Name}] stream read interrupted (attempt {Attempt}); retrying in {DelaySec:0.0}s",
                    name, attempt, delay.TotalSeconds);
                try { await Task.Delay(delay, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        logger.LogInformation("[{Name}] consumer stopped", name);
    }

    /// <summary>지수 백오프 + 지터: base,2×,4×,…(최대 maxMs) ±20%.</summary>
    private static TimeSpan Backoff(int attempt, double baseMs, double maxMs)
    {
        double exp = baseMs * Math.Pow(2, Math.Min(attempt - 1, 10));
        double capped = Math.Min(exp, maxMs);
        double jitter = capped * 0.2 * (Random.Shared.NextDouble() * 2 - 1); // ±20%
        return TimeSpan.FromMilliseconds(Math.Max(baseMs, capped + jitter));
    }
}
