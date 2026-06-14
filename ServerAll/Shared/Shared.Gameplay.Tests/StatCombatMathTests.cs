using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// 스탯 기반 데미지 산식(2.4) — max(1, baseDamage + attackPower - defense). 결정론·클라 미러.
/// </summary>
public class StatCombatMathTests
{
    [Fact]
    public void 공격력만큼_base에_가산된다()
    {
        Assert.Equal(30, StatCombatMath.MeleeDamage(baseDamage: 10, attackPower: 20, defense: 0));
    }

    [Fact]
    public void 방어력만큼_감산된다()
    {
        Assert.Equal(22, StatCombatMath.MeleeDamage(baseDamage: 10, attackPower: 20, defense: 8));
    }

    [Fact]
    public void 공격력_0이면_base_그대로다_하위호환()
    {
        // AttackPower 미설정(0) = 기존 고정값 전투와 동일 — 회귀 보호.
        Assert.Equal(10, StatCombatMath.MeleeDamage(baseDamage: 10, attackPower: 0, defense: 0));
    }

    [Fact]
    public void 방어가_공격을_넘어도_최소_1은_들어간다()
    {
        Assert.Equal(1, StatCombatMath.MeleeDamage(baseDamage: 5, attackPower: 0, defense: 100));
    }
}
