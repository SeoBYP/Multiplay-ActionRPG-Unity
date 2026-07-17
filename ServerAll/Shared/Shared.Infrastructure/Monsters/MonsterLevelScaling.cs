namespace Shared.Infrastructure.Monsters;

/// <summary>몬스터 등급. 레벨과 **직교** — 레벨은 "어느 던전 대역인가", 등급은 "그 대역 안에서 얼마나 강한가".</summary>
public enum MonsterTier
{
    Normal = 0,
    Elite = 1,
    Boss = 2,
}

/// <summary>
/// 몬스터 스탯의 레벨·등급 스케일(AC-E). 설계 = <c>docs/wiki/monster-leveling.md</c>.
///
/// <para><b>왜 필요한가</b>: 플레이어만 선형 성장(<c>DEF = 5+2(L-1)</c>)하고 몬스터는 고정이라
/// <c>max(1, base − DEF)</c> 가 L19 부터 전 몬스터를 1 데미지로 눌렀다(C1c 실측: 1,1,2,2,3,5).</para>
///
/// <para><b>왜 Infrastructure 인가</b>: 이 식은 <b>플레이어 레벨 곡선을 정의상 참조</b>한다(DEF 성장·HP 성장비).
/// 곡선이 있는 <c>Progression/LevelTable</c> 옆이 맞다 — 순수 산식인 <c>Shared.Gameplay/StatCombatMath</c> 에 두면
/// 테이블 역참조가 된다.</para>
///
/// <para><b>StatCombatMath 는 건드리지 않는다</b> — 산식(<c>max(1, base+AP−DEF)</c>)은 옳았고 틀린 건 base 였다.
/// 여기서 base 만 스케일해 넣는다.</para>
/// </summary>
public static class MonsterLevelScaling
{
    // ── 플레이어 곡선에서 유도된 상수(level-table.json 과 일치해야 한다) ──
    // 곡선을 바꾸면 여기도 같이 바꾼다 — 어긋나면 밸런스가 조용히 틀어진다(리뷰 대상).

    /// <summary>플레이어 L1 방어력. 순피해 <c>net₁ = base₁ − BaseDefense</c> 의 기준.</summary>
    public const int PlayerBaseDefense = 5;

    /// <summary>플레이어 레벨당 방어력 증가. <b>몬스터 피해 증가폭의 하한</b> — 이보다 낮으면 언젠가 바닥에 눌린다.</summary>
    public const int PlayerDefensePerLevel = 2;

    /// <summary>플레이어 HP 성장비(20/100). 순피해가 이 비율로 커져야 "체감 난이도 불변".</summary>
    public const float PlayerHpGrowthRatio = 0.2f;

    /// <summary>플레이어 AP 성장비(3/10). 몬스터 HP 가 이 비율로 커져야 킬 타임이 유지된다.</summary>
    public const float PlayerAttackGrowthRatio = 0.3f;

    /// <summary>
    /// 레벨 L·등급 T 의 피해 base.
    ///
    /// <para><b>비례 가산</b>: <c>base(L) = base₁ + (DefPerLevel + HpGrowthRatio·net₁)(L−1)</c>
    /// — 증가폭이 <c>net₁</c> 에 비례해 <b>각 몬스터의 역할이 보존</b>된다.</para>
    ///
    /// <para>대안을 왜 버렸나:
    /// <list type="bullet">
    /// <item><b>곱셈</b>(<c>base×(1+k(L-1))</c>) — base 가 큰 어빌리티에서 폭발한다. slam(90) 이 L20 에 688 → 플레이어 HP 480 즉사.</item>
    /// <item><b>단순 가산</b>(<c>base+4(L-1)</c>) — 약한 몬스터는 세지고 강한 몬스터는 약해져 <b>전부 중간으로 수렴</b>(역할 붕괴).</item>
    /// </list></para>
    /// </summary>
    public static int Damage(int baseDamage, int level, MonsterTier tier = MonsterTier.Normal)
    {
        float scaled = ScaleByLevel(baseDamage, level);
        return Math.Max(1, (int)MathF.Round(scaled * DamageMultiplier(tier)));
    }

    /// <summary>
    /// 레벨 L·등급 T 의 최대 HP. 플레이어 AP 성장(<c>+3/L</c>)에 맞춰 커져야 킬 타임이 유지된다.
    /// </summary>
    public static int Hp(int baseHp, int level, MonsterTier tier = MonsterTier.Normal)
    {
        int lv = NormalizeLevel(level);
        float scaled = baseHp * (1f + PlayerAttackGrowthRatio * (lv - 1));
        return Math.Max(1, (int)MathF.Round(scaled * HpMultiplier(tier)));
    }

    /// <summary>레벨 L·등급 T 의 경험치 보상. 레벨 비례 + 등급 배율.</summary>
    public static long Exp(long baseExp, int level, MonsterTier tier = MonsterTier.Normal)
    {
        if (baseExp <= 0) return 0; // 보상 없는 몬스터(테스트용 등)는 스케일해도 0
        int lv = NormalizeLevel(level);
        float scaled = baseExp * (1f + PlayerHpGrowthRatio * (lv - 1));
        return (long)MathF.Round(scaled * ExpMultiplier(tier));
    }

    /// <summary>드롭 확률 배율(등급). 상위 등급일수록 잘 떨군다.</summary>
    public static float DropChanceMultiplier(MonsterTier tier) => tier switch
    {
        MonsterTier.Elite => 2f,
        MonsterTier.Boss => 3f,
        _ => 1f,
    };

    /// <summary>골드 등 수량 드롭의 레벨 배율. 보상 감각이 레벨과 함께 커진다.</summary>
    public static float DropQuantityMultiplier(int level)
        => 1f + PlayerHpGrowthRatio * (NormalizeLevel(level) - 1);

    /// <summary>
    /// 등급 배율 — <b>HP 를 크게, 피해를 작게</b> 올린다.
    /// 피해를 크게 올리면 즉사가 되고, HP 를 올리면 "오래 버티는 위협"이 된다(액션 RPG 관례).
    /// </summary>
    private static float HpMultiplier(MonsterTier tier) => tier switch
    {
        MonsterTier.Elite => 2f,
        MonsterTier.Boss => 6f,
        _ => 1f,
    };

    private static float DamageMultiplier(MonsterTier tier) => tier switch
    {
        MonsterTier.Elite => 1.3f,
        MonsterTier.Boss => 1.6f,
        _ => 1f,
    };

    private static float ExpMultiplier(MonsterTier tier) => tier switch
    {
        MonsterTier.Elite => 3f,
        MonsterTier.Boss => 10f,
        _ => 1f,
    };

    /// <summary>레벨 기준 피해 base(등급 배율 제외). 유도 = monster-leveling.md §2.</summary>
    private static float ScaleByLevel(int baseDamage, int level)
    {
        int lv = NormalizeLevel(level);
        float net1 = baseDamage - PlayerBaseDefense;                 // L1 순피해
        float perLevel = PlayerDefensePerLevel + PlayerHpGrowthRatio * net1;
        return baseDamage + perLevel * (lv - 1);
    }

    /// <summary>미저작(0)·음수는 L1 로 본다 — 데이터 누락이 스탯 붕괴로 번지지 않게.</summary>
    private static int NormalizeLevel(int level) => level < 1 ? 1 : level;
}
