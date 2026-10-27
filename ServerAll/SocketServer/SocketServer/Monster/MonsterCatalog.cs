using Shared.Infrastructure.Abilities;
using SharedMonsters = Shared.Infrastructure.Monsters;

namespace Server.Monster;

// AttackRange = 어빌리티들의 최대 ActivationRange(파생). 공격 수치는 어빌리티가 갖는다.
public sealed record MonsterStats(
    int MaxHp,
    float MoveSpeed,    // units/sec
    float AggroRange,
    float AttackRange);

// 진실원 = Shared MonsterCatalog + AbilityCatalog. 미등록 타입은 Shared Default 로 폴백(어빌리티 없음 = 공격 안 함).
public static class MonsterCatalog
{
    public static IReadOnlyList<AbilityDef> GetAbilities(string? monsterId)
    {
        var def = SharedMonsters.MonsterCatalog.Get(monsterId);
        var list = new List<AbilityDef>(def.AbilityIds.Count);
        foreach (var id in def.AbilityIds)
        {
            var ability = AbilityCatalog.Get(id);
            if (ability != null) list.Add(ability);
        }
        return list;
    }

    public static MonsterStats Get(string? monsterId)
    {
        var def = SharedMonsters.MonsterCatalog.Get(monsterId);

        // Attack 페이즈 진입 거리 = 어빌리티 중 가장 먼 사거리(없으면 0 = 접근만).
        float attackRange = 0f;
        foreach (var ability in GetAbilities(monsterId))
            if (ability.ActivationRange > attackRange)
                attackRange = ability.ActivationRange;

        return new MonsterStats(def.MaxHp, def.MoveSpeed, def.AggroRange, attackRange);
    }
}
