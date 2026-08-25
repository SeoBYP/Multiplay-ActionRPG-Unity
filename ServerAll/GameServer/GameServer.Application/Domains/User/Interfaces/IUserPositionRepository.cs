using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.User.Interfaces;

/// <summary>
/// Main 위치 저장소.
///
/// ⚠ **이 도메인만 cache-aside 교리의 예외다.** 다른 저장소는 "DB 저장 → 캐시 DEL" 인데,
/// 위치는 쓰기가 매우 잦고(주기 보고) 유실이 허용되는 유일한 데이터라 **Redis 를 1차 저장소**로 쓴다.
/// 확정(DB)은 이탈 시점에만 한 번. 그래서 유실 폭이 "마지막 확정 이후"로 한정되고 명시적이다.
/// 이 예외를 다른 도메인에 복사하지 말 것 — networking.md 에 사유와 함께 적어 두었다.
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
