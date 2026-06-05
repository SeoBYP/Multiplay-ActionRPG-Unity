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

    private static async IAsyncEnumerable<int> FailingStream()
    {
        await Task.Yield();
        if (Guard) throw new InvalidOperationException("transient read failure");
        yield return 0;
    }

    private static async IAsyncEnumerable<int> YieldStream(params int[] items)
    {
        foreach (var i in items)
        {
            await Task.Yield();
            yield return i;
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
}
