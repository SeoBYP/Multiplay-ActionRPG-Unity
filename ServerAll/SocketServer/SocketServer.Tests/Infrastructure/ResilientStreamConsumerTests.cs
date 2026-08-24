using Microsoft.Extensions.Logging.Abstractions;
using Shared.Infrastructure.MessageQueue;

namespace Server.Tests.Infrastructure;

/// <summary>
/// 스트림 컨슈머 복원력 계약: 읽기 실패에 죽지 않고 재시도, poison 메시지는 격리, 취소는 정상 종료.
/// </summary>
public class ResilientStreamConsumerTests
{
    private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(1);

    private static bool Guard => true; // 컴파일러가 상수폴딩 못 하게 — 항상 throw 하는 이터레이터용

    private static async IAsyncEnumerable<StreamMessage<int>> FailingStream()
    {
        await Task.Yield();
        if (Guard) throw new InvalidOperationException("transient read failure");
        yield return StreamMessage<int>.WithoutAck(0);
    }

    private static async IAsyncEnumerable<StreamMessage<int>> YieldStream(params int[] items)
    {
        foreach (var i in items)
        {
            await Task.Yield();
            yield return StreamMessage<int>.WithoutAck(i);
        }
    }

    /// <summary>ACK 호출을 기록하는 봉투 스트림 — "언제 ACK 되는가" 를 관측한다.</summary>
    private static async IAsyncEnumerable<StreamMessage<int>> AckTrackingStream(List<int> acked, params int[] items)
    {
        foreach (var i in items)
        {
            await Task.Yield();
            var captured = i;
            yield return new StreamMessage<int>(captured, () =>
            {
                acked.Add(captured);
                return Task.CompletedTask;
            });
        }
    }

    [Fact]
    public async Task 스트림_읽기가_실패하면_죽지않고_재시도해_처리한다()
    {
        var handled = new List<int>();
        using var cts = new CancellationTokenSource();
        int call = 0;

        await ResilientStreamConsumer.RunAsync<int>(
            "test",
            _ => Interlocked.Increment(ref call) == 1 ? FailingStream() : YieldStream(42),
            (msg, _) => { handled.Add(msg); cts.Cancel(); return Task.CompletedTask; },
            NullLogger.Instance,
            cts.Token,
            baseDelay: Fast, maxDelay: Fast);

        Assert.Equal(new[] { 42 }, handled); // 첫 읽기 실패 후 재시도해 처리
        Assert.True(call >= 2);              // 죽지 않고 재시도함
    }

    [Fact]
    public async Task 메시지_핸들러가_실패해도_다음_메시지를_계속_처리한다()
    {
        var handled = new List<int>();
        using var cts = new CancellationTokenSource();

        await ResilientStreamConsumer.RunAsync<int>(
            "test",
            _ => YieldStream(1, 2, 3),
            (msg, _) =>
            {
                if (msg == 2) throw new InvalidOperationException("poison");
                handled.Add(msg);
                if (msg == 3) cts.Cancel();
                return Task.CompletedTask;
            },
            NullLogger.Instance,
            cts.Token,
            baseDelay: Fast, maxDelay: Fast);

        Assert.Equal(new[] { 1, 3 }, handled); // 2는 건너뛰고 1,3 처리(poison 격리)
    }

    [Fact]
    public async Task 취소되면_정상_종료한다()
    {
        var run = ResilientStreamConsumer.RunAsync<int>(
            "test",
            _ => YieldStream(1, 2, 3),
            (_, _) => Task.CompletedTask,
            NullLogger.Instance,
            new CancellationToken(canceled: true),
            baseDelay: Fast, maxDelay: Fast);

        var finished = await Task.WhenAny(run, Task.Delay(2000));
        Assert.Same(run, finished); // 행 없이 즉시 종료
        await run;
    }

    [Fact]
    public async Task 핸들러가_성공한_메시지만_ACK_한다()
    {
        var acked = new List<int>();
        using var cts = new CancellationTokenSource();

        await ResilientStreamConsumer.RunAsync<int>(
            "test",
            _ => AckTrackingStream(acked, 1, 2, 3),
            (msg, _) =>
            {
                if (msg == 2) throw new InvalidOperationException("handler failed");
                if (msg == 3) cts.Cancel();
                return Task.CompletedTask;
            },
            NullLogger.Instance,
            cts.Token,
            baseDelay: Fast, maxDelay: Fast);

        // 2 는 핸들러가 던졌으므로 ACK 되면 안 된다 — ACK 하면 재배달 없이 영구 소실된다(at-most-once).
        Assert.Equal(new[] { 1, 3 }, acked);
    }

    [Fact]
    public async Task ACK_가_실패해도_스트림은_계속_돈다()
    {
        var handled = new List<int>();
        using var cts = new CancellationTokenSource();

        static async IAsyncEnumerable<StreamMessage<int>> FlakyAckStream()
        {
            await Task.Yield();
            yield return new StreamMessage<int>(1, () => throw new InvalidOperationException("ack failed"));
            await Task.Yield();
            yield return new StreamMessage<int>(2, () => Task.CompletedTask);
        }

        await ResilientStreamConsumer.RunAsync<int>(
            "test",
            _ => FlakyAckStream(),
            (msg, _) => { handled.Add(msg); if (msg == 2) cts.Cancel(); return Task.CompletedTask; },
            NullLogger.Instance,
            cts.Token,
            baseDelay: Fast, maxDelay: Fast);

        // ACK 실패는 재배달로 이어질 뿐(핸들러 멱등 전제), 컨슈머를 죽이면 안 된다.
        Assert.Equal(new[] { 1, 2 }, handled);
    }
}
