using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.Progression.Interfaces;

/// <summary>
/// 플레이어 진행(레벨·경험치) 영속 저장소. Cache-Aside + Delete 패턴.
/// row 는 첫 적립 때 lazy 생성한다(회원가입은 만들지 않는다).
/// </summary>
public interface IProgressionRepository
{
    /// <summary>진행 조회(캐시 우선). row 가 없으면 null — 생성하지 않는다.</summary>
    Task<UserProgression?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>경험치 누적 적립. row 가 없으면 생성. DB 저장 후 캐시 DEL. 갱신된 진행 반환.</summary>
    Task<UserProgression> AddExpAsync(long userId, long amount, CancellationToken ct = default);
}
