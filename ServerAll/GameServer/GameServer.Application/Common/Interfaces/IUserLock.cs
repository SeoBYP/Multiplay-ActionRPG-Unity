namespace GameServer.Application.Common.Interfaces;

public interface IUserLock
{
    /// <summary>
    /// 락 획득. await using으로 사용하면 자동 해제.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(string lockKey, CancellationToken ct = default);
}