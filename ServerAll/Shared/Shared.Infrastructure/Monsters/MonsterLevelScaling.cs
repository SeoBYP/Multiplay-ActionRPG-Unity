using Shared.Infrastructure.Progression;

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
/// <para><b>왜 필요한가</b>: 플레이어만 성장하고 몬스터는 고정이라 <c>max(1, base − DEF)</c> 가
/// 고레벨에서 전 몬스터를 1 데미지로 눌렀다(C1c 실측: 1,1,2,2,3,5).</para>
///
/// <para><b>상수가 없다 — 전부 저작 테이블에서 읽는다(AC-F1)</b>:
/// <list type="bullet">
/// <item>플레이어 곡선 = <see cref="LevelTable"/>(SO <c>LevelTableDefinition</c> → level-table.json)</item>
/// <item>등급 배율 = <see cref="MonsterScalingCatalog"/>(SO <c>MonsterScalingDefinition</c> → monster-scaling.json)</item>
/// </list>
/// 이전엔 곡선 상수(DEF 5/+2, HP비 0.2…)를 여기 하드코딩하고 "곡선 바꾸면 여기도 같이 바꿔라"는
/// 주석을 달았다 — 그게 바로 SO 교리가 막으려는 **수동 동기화 함정**이었다.</para>
///
/// <para><b>StatCombatMath 는 건드리지 않는다</b> — 산식(<c>max(1, base+AP−DEF)</c>)은 옳았고 틀린 건 base 였다.</para>
/// </summary>
public static class MonsterLevelScaling
{
    /// <summary>
    /// 레벨 L·등급 T 의 피해 base.
    ///
    /// <para><b>유도</b>(monster-leveling.md §2) — "체감 난이도 불변" = 순피해가 플레이어 HP 에 비례:
    /// <code>
    /// 목표:  net(L) / HP(L) = net₁ / HP(1)
    ///   ⇒  base(L) = net₁ · HP(L)/HP(1) + DEF(L)
    /// </code>
    /// <b>테이블을 직접 읽으므로 곡선이 비선형으로 바뀌어도 자동으로 따라간다.</b></para>
    ///
    /// <para>대안을 왜 버렸나:
    /// <list type="bullet">
    /// <item><b>곱셈</b>(<c>base×(1+k(L-1))</c>) — base 가 큰 어빌리티에서 폭발. slam(90) 이 L20 에 688 → 플레이어 HP 480 즉사.</item>
    /// <item><b>단순 가산</b>(<c>base+4(L-1)</c>) — 약한 몬스터는 세지고 강한 몬스터는 약해져 <b>전부 중간으로 수렴</b>(역할 붕괴).</item>
    /// </list></para>
    /// </summary>
    public static int Damage(int baseDamage, int level, MonsterTier tier = MonsterTier.Normal)
    {
        int lv = NormalizeLevel(level);
        var l1 = LevelTable.StatsAt(1);
        var lN = LevelTable.StatsAt(lv);

        float net1 = baseDamage - l1.Defense;                 // L1 순피해 = 이 몬스터의 "역할"
        float scaled = net1 * ((float)lN.MaxHealth / l1.MaxHealth) + lN.Defense;

        return Math.Max(1, (int)MathF.Round(scaled * MonsterScalingCatalog.Get(tier).DamageMultiplier));
    }

    /// <summary>
    /// 레벨 L·등급 T 의 최대 HP. <b>플레이어 공격력 성장에 비례</b>해야 킬 타임이 유지된다:
    /// <c>maxHp(L) = maxHp₁ · AP(L)/AP(1)</c>.
    /// </summary>
    public static int Hp(int baseHp, int level, MonsterTier tier = MonsterTier.Normal)
    {
        int lv = NormalizeLevel(level);
        var l1 = LevelTable.StatsAt(1);
        var lN = LevelTable.StatsAt(lv);

        float scaled = baseHp * ((float)lN.AttackPower / l1.AttackPower);
        return Math.Max(1, (int)MathF.Round(scaled * MonsterScalingCatalog.Get(tier).HpMultiplier));
    }

    /// <summary>레벨 L·등급 T 의 경험치 보상. 플레이어 HP 성장에 비례(= 보상 감각 유지) + 등급 배율.</summary>
    public static long Exp(long baseExp, int level, MonsterTier tier = MonsterTier.Normal)
    {
        if (baseExp <= 0) return 0; // 보상 없는 몬스터(테스트 픽스처 등)는 스케일해도 0
        float scaled = baseExp * LevelGrowth(level);
        return (long)MathF.Round(scaled * MonsterScalingCatalog.Get(tier).ExpMultiplier);
    }

    /// <summary>드롭 확률 배율(등급). 상위 등급일수록 잘 떨군다.</summary>
    public static float DropChanceMultiplier(MonsterTier tier)
        => MonsterScalingCatalog.Get(tier).DropChanceMultiplier;

    /// <summary>골드 등 <b>가변 수량</b> 드롭의 레벨 배율. 보상 감각이 레벨과 함께 커진다.</summary>
    public static float DropQuantityMultiplier(int level) => LevelGrowth(level);

    /// <summary>플레이어 HP 성장비 = <c>HP(L)/HP(1)</c>. 보상·순피해가 이 비율을 따라간다.</summary>
    private static float LevelGrowth(int level)
    {
        int lv = NormalizeLevel(level);
        return (float)LevelTable.StatsAt(lv).MaxHealth / LevelTable.StatsAt(1).MaxHealth;
    }

    /// <summary>미저작(0)·음수는 L1 로 본다 — 데이터 누락이 스탯 붕괴로 번지지 않게.</summary>
    private static int NormalizeLevel(int level) => level < 1 ? 1 : level;
}
