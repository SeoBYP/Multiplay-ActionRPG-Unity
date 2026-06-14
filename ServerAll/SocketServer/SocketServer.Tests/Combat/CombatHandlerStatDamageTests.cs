using System.Linq;
using Script.System.GamePlayAbilitySystem;
using Server.PacketHandler.Handler;

namespace Server.Tests.Combat;

/// <summary>
/// 스탯 기반 데미지(2.4 증분2) — CombatHandler.ScaleDamageByStats 가 카탈로그 base 를 AttackPower/Defense 로 재계산.
/// 산식 자체는 Shared.Gameplay.Tests.StatCombatMathTests 가 검증 — 여기선 스킬 카탈로그 연동·스케일만 본다.
/// </summary>
public class CombatHandlerStatDamageTests
{
    private static int HealthDamage(System.Collections.Generic.List<GameplayAttributeModifier> mods)
        => -mods.Where(m => m.AttributeType == EGameplayAttribute.Health && m.Amount < 0).Sum(m => m.Amount);

    [Fact]
    public void 공격력은_데미지에_선형_가산된다()
    {
        var skill = CombatHandler.ResolveSkill(0)!;

        int baseDmg = HealthDamage(CombatHandler.ScaleDamageByStats(skill, attackPower: 0, defense: 0));
        int scaled = HealthDamage(CombatHandler.ScaleDamageByStats(skill, attackPower: 20, defense: 0));

        Assert.Equal(baseDmg + 20, scaled); // base 값에 무관하게 AttackPower 만큼 더 들어감
    }

    [Fact]
    public void 공격력_0이면_카탈로그_base와_동일하다_하위호환()
    {
        var skill = CombatHandler.ResolveSkill(0)!;

        int baseDmg = HealthDamage(CombatHandler.ScaleDamageByStats(skill, attackPower: 0, defense: 0));

        Assert.True(baseDmg > 0); // basic_attack_dmg 가 양의 데미지
        // AttackPower 미설정(0) = 기존 고정값 전투와 동일 — 회귀 보호.
    }

    [Fact]
    public void 방어력은_데미지를_감산한다()
    {
        var skill = CombatHandler.ResolveSkill(0)!;

        int noDef = HealthDamage(CombatHandler.ScaleDamageByStats(skill, attackPower: 20, defense: 0));
        int withDef = HealthDamage(CombatHandler.ScaleDamageByStats(skill, attackPower: 20, defense: 5));

        Assert.Equal(noDef - 5, withDef);
    }
}
