using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

/// <summary>
/// Main 위치 저장소. 이 도메인만 cache-aside 의 예외다 — 쓰기가 잦고 유실이 허용되므로 Redis 가 1차 저장소이고,
/// DB 확정은 이탈 시점에 한 번이다(유실 폭 = 마지막 확정 이후). 다른 도메인에 복사하지 말 것.
/// </summary>
public interface IUserPositionRepository
{
    /// <summary>휘발 저장(Redis). 주기 보고 경로.</summary>
    Task SaveVolatileAsync(UserPosition position, CancellationToken ct = default);

    /// <summary>Redis → 없으면 DB. 둘 다 없으면 null.</summary>
    Task<UserPosition?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>휘발 저장소의 최신값을 DB 로 확정(UPSERT). 없으면 no-op.</summary>
    Task FlushToDatabaseAsync(long userId, CancellationToken ct = default);
}
