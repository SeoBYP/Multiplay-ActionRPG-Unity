using SharedMonsters = Shared.Infrastructure.Monsters;

namespace Server.Monster;

/// <summary>몬스터 타입별 스탯(서버 시뮬레이션 전용 뷰). exp 는 시뮬에 불필요해 제외.</summary>
public sealed record MonsterStats(
    int MaxHp,
    float MoveSpeed,        // units/sec
    float AggroRange,       // 추격 시작 거리
    float AttackRange,      // 정지·공격 거리
    float AttackCooldownMs, // 공격 간격
    int AttackDamage,
    string OnHitEffectId = ""); // CC: 적중 시 부여할 효과 id(빈 문자열=없음)

/// <summary>
/// monsterId → MonsterStats. 데이터 진실원 = Shared `MonsterCatalog`(SO 저작 → bake monsters.json).
/// 이 클래스는 던전 시뮬이 쓰는 스탯만 추려 매핑하는 얇은 어댑터다(exp 는 GameServer Main 경로에서만 사용).
/// 미등록 타입은 Shared Default 로 폴백.
/// </summary>
public static class MonsterCatalog
{
    public static MonsterStats Get(string? monsterId)
    {
        var def = SharedMonsters.MonsterCatalog.Get(monsterId);
        return new MonsterStats(
            def.MaxHp, def.MoveSpeed, def.AggroRange,
            def.AttackRange, def.AttackCooldownMs, def.AttackDamage, def.OnHitEffectId);
    }
}
