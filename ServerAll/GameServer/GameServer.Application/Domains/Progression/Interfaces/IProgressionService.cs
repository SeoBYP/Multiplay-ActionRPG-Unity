using GameServer.Domain.Entities.User;

namespace GameServer.Application.Domains.Progression.Interfaces;

/// <summary>
/// 진행(레벨·경험치) 도메인 서비스. 던전 보상 지급·스탯창 조회 등 상위 흐름이 호출한다.
/// </summary>
public interface IProgressionService
{
    /// <summary>한 유저에게 경험치를 적립한다(0 이하는 무시·레벨업은 저장소 내부). 갱신된 현재 레벨의 Exp 를 반환.</summary>
    Task<long> AddExpAsync(long userId, long amount, CancellationToken ct = default);

    /// <summary>
    /// 유저의 진행(레벨·Exp)을 읽는다. 아직 적립 이력이 없으면 기본값(Lv1/Exp0) 뷰를 반환(영속하지 않음).
    /// </summary>
    Task<UserProgression> GetProgressionAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 유저의 **합산 전투 스탯**(서버 권위)을 계산한다. 오늘 = 레벨 테이블 룩업.
    /// 미래 장비(3.2)/버프 합류점 = **단일 합산 권위**(authority-model §4c).
    /// SocketServer 스탯 전파(게임시작 메시지)·스탯창(GetProgression)이 사용한다.
    /// </summary>
    Task<PlayerStats> GetStatsAsync(long userId, CancellationToken ct = default);
}
