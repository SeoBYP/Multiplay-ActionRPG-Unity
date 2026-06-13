namespace GameServer.Application.Domains.Inventory.Interfaces;

/// <summary>
/// Main 킬 클레임(B-lite)의 per-(user,slot) 쿨다운 점유 저장소. 재청구 차단의 원자적 게이트.
/// 구현 = Redis `SET NX PX`(키 없으면 점유+TTL, 있으면 거부). 단위테스트는 인메모리 fake 로 교체한다
/// (Redis 의존 없이 슬롯/쿨다운/roll 로직 검증). 인터페이스 도입 기준 = 테스트 Stub 교체.
/// </summary>
public interface IClaimCooldownStore
{
    /// <summary>키가 비어 있으면 ttl 동안 점유하고 true(클레임 성공). 이미 점유 중(쿨다운)이면 false. 원자적.</summary>
    Task<bool> TryClaimAsync(string key, TimeSpan ttl, CancellationToken ct = default);
}
