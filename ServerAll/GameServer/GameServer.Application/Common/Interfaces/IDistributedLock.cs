namespace GameServer.Application.Common.Interfaces;

/// <summary>
/// 프로세스 간 상호배제. 스코프(유저·방 등)는 <paramref name="lockKey"/> 로 표현한다.
/// 구현체가 키에 자체 네임스페이스를 덧붙이므로 호출자는 스코프만 적는다 (예: "room:12", "chat:user:3").
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// 락 획득. await using으로 사용하면 자동 해제.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(string lockKey, CancellationToken ct = default);
}
