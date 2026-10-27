namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// Main 킬 클레임의 per-(user,slot) 쿨다운 점유 저장소. 재청구 차단의 원자적 게이트.
/// 구현은 Redis `SET NX PX`, 단위테스트는 인메모리 fake 로 교체한다.
/// </summary>
public interface IClaimCooldownStore
{
    /// <summary>키가 비어 있으면 ttl 동안 점유하고 true(클레임 성공). 이미 점유 중(쿨다운)이면 false. 원자적.</summary>
    Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}
