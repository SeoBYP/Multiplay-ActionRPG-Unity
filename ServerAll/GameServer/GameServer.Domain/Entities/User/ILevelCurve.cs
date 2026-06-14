namespace GameServer.Domain.Entities.User;

/// <summary>
/// 레벨업 임계 곡선(도메인 추상). <see cref="UserProgression.AddExp"/> 가 레벨업 판정에 쓴다.
///
/// 곡선 *수치*는 기획 데이터(레벨 테이블)라 Infrastructure 관심사 — Domain 은 계약만 안다(DIP).
/// 구현: Infrastructure `LevelTableCurve`(실데이터, 임베디드 JSON) / 테스트 스텁(제어 가능한 임계).
/// </summary>
public interface ILevelCurve
{
    /// <summary>최고 레벨. 이 레벨에 도달하면 레벨업을 멈춘다(만렙 고정).</summary>
    int MaxLevel { get; }

    /// <summary>현재 레벨 → 다음 레벨까지 필요한 Exp.</summary>
    long ExpToNext(int level);
}
