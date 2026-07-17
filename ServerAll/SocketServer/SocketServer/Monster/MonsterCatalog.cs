using Shared.Infrastructure.Abilities;
using SharedMonsters = Shared.Infrastructure.Monsters;

namespace Server.Monster;

/// <summary>
/// 몬스터 타입별 스탯(서버 시뮬레이션 전용 뷰). exp 는 시뮬에 불필요해 제외.
/// <para>
/// AC-B B4: 공격 수치(쿨다운·데미지·CC)는 여기 없다 — **어빌리티**(<see cref="AbilityDef"/>)가 갖는다.
/// <see cref="AttackRange"/> 만 AI(Attack 페이즈 진입 판정)를 위해 어빌리티들의 **최대 사거리**로 파생한다.
/// </para>
/// </summary>
public sealed record MonsterStats(
    int MaxHp,
    float MoveSpeed,    // units/sec
    float AggroRange,   // 추격 시작 거리
    float AttackRange); // 정지·공격 거리 = 이 몬스터 어빌리티들의 최대 ActivationRange(파생)

/// <summary>
/// monsterId → MonsterStats / 어빌리티 목록. 데이터 진실원 = Shared `MonsterCatalog`(SO 저작 → bake monsters.json)
/// + `AbilityCatalog`(SO 저작 → bake abilities.json). 던전 시뮬이 쓰는 형태로 추리는 얇은 어댑터다.
/// 미등록 타입은 Shared Default 로 폴백(어빌리티 없음 = 공격 안 함).
/// </summary>
public static class MonsterCatalog
{
    /// <summary>이 몬스터가 쓰는 어빌리티(저작 순서 = 발동 우선순위). 미등록 id 는 건너뛴다.</summary>
    public static IReadOnlyList<AbilityDef> GetAbilities(string? monsterId)
    {
        var def = SharedMonsters.MonsterCatalog.Get(monsterId);
        var list = new List<AbilityDef>(def.AbilityIds.Count);
        foreach (var id in def.AbilityIds)
        {
            var ability = AbilityCatalog.Get(id);
            if (ability != null) list.Add(ability); // 저작 오타 등 미등록은 조용히 무시(시뮬은 계속 돈다)
        }
        return list;
    }

    public static MonsterStats Get(string? monsterId)
    {
        var def = SharedMonsters.MonsterCatalog.Get(monsterId);

        // AI 의 Attack 페이즈 진입 거리 = 쓸 수 있는 어빌리티 중 가장 먼 사거리.
        // (어빌리티가 없으면 0 = 접근만 하고 공격은 안 함)
        float attackRange = 0f;
        foreach (var ability in GetAbilities(monsterId))
            if (ability.ActivationRange > attackRange)
                attackRange = ability.ActivationRange;

        return new MonsterStats(def.MaxHp, def.MoveSpeed, def.AggroRange, attackRange);
    }
}
