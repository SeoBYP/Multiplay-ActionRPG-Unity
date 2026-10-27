using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.Progression.Interfaces;

public interface IProgressionService
{
    /// <summary>경험치 적립(0 이하는 무시, 레벨업은 저장소 내부). 갱신된 현재 레벨의 Exp 반환.</summary>
    Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default);

    /// <summary>진행 조회. 적립 이력이 없으면 기본값(Lv1/Exp0) 뷰를 반환한다(영속하지 않음).</summary>
    Task<UserProgression> GetProgressionAsync(long userId, CancellationToken ct = default);

    /// <summary>합산 전투 스탯(서버 권위) 계산. 게임시작 메시지와 스탯창이 사용하는 단일 합산 권위.</summary>
    Task<PlayerStats> GetStatsAsync(long userId, CancellationToken ct = default);
}
