using GameServer.Application.Common.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes;

/// <summary>테스트용 무동작 락. 단일 스레드 단위 테스트에서 상호배제는 검증 대상이 아니다.</summary>
public sealed class NoOpDistributedLock : IDistributedLock
{
    public Task<IAsyncDisposable> AcquireAsync(string lockKey, CancellationToken ct = default)
        => Task.FromResult<IAsyncDisposable>(new Releaser());

    private sealed class Releaser : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
