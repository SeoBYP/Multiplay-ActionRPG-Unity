using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.Progression.Interfaces;

/// <summary>
/// 플레이어 진행(레벨·경험치) 영속 저장소. Cache-Aside + Delete 패턴.
///
/// 던전 보상은 첫 클리어 시점에야 row 가 필요하므로 lazy get-or-create 로 동작한다
/// (회원가입 흐름은 progression row 를 만들지 않는다).
/// </summary>
public interface IProgressionRepository
{
    /// <summary>진행을 읽는다(캐시 우선). row 가 없으면 null — 생성하지 않는다.</summary>
    Task<UserProgression?> GetAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 경험치를 누적 적립하고 영속한다. row 가 없으면 생성(lazy)한다.
    /// DB 저장 후 캐시는 DEL(덮어쓰지 않음). 갱신된 진행을 반환.
    /// </summary>
    Task<UserProgression> AddExpAsync(long userId, long amount, CancellationToken ct = default);
}
