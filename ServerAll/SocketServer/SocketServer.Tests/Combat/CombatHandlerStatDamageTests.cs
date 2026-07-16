using System.Linq;
using Script.System.GamePlayAbilitySystem;
using Server.PacketHandler.Handler;

namespace Server.Tests.Combat;

/// <summary>
/// 스탯 기반 데미지(2.4 + AC-B 안B) — `CombatHandler.BuildDamageMods` 가 **어빌리티 baseDamage** 를
/// AttackPower/Defense 로 재계산한다(구 ScaleDamageByStats 의 effect-카탈로그 경로 대체).
/// 산식 자체는 Shared.Gameplay.Tests.StatCombatMathTests 가 검증 — 여기선 어빌리티 연동·스케일만 본다.
/// </summary>
public class CombatHandlerStatDamageTests
{
    private static int HealthDamage(System.Collections.Generic.List<GameplayAttributeModifier> mods)
        => -mods.Where(m => m.AttributeType == EGameplayAttribute.Health && m.Amount < 0).Sum(m => m.Amount);

    [Fact]
    public void 공격력은_데미지에_선형_가산된다()
    {
        var ability = CombatHandler.ResolveAbility(0)!;

        int baseDmg = HealthDamage(CombatHandler.BuildDamageMods(ability, attackPower: 0, defense: 0));
        int scaled = HealthDamage(CombatHandler.BuildDamageMods(ability, attackPower: 20, defense: 0));

        Assert.Equal(baseDmg + 20, scaled); // base 값에 무관하게 AttackPower 만큼 더 들어감
    }

    [Fact]
    public void 공격력_0이면_어빌리티_baseDamage와_동일하다()
    {
        var ability = CombatHandler.ResolveAbility(0)!;

        int baseDmg = HealthDamage(CombatHandler.BuildDamageMods(ability, attackPower: 0, defense: 0));

        // AC-B 안B: 데미지 출처 = ability.baseDamage(effect 카탈로그 수치가 아니라).
        // AttackPower 미설정(0) = 저작한 base 그대로 — 이관 시 밸런스 무변경의 근거.
        Assert.Equal(ability.BaseDamage, baseDmg);
        Assert.True(baseDmg > 0);
    }

    [Fact]
    public void 방어력은_데미지를_감산한다()
    {
        var ability = CombatHandler.ResolveAbility(0)!;

        int noDef = HealthDamage(CombatHandler.BuildDamageMods(ability, attackPower: 20, defense: 0));
        int withDef = HealthDamage(CombatHandler.BuildDamageMods(ability, attackPower: 20, defense: 5));

        Assert.Equal(noDef - 5, withDef);
    }

    [Fact]
    public void 데미지는_어빌리티마다_저작값을_따른다()
    {
        // 콤보 A(10) < B(15) < C(25) — 수치가 Ability SO 단일 저작임을 고정(effect 카탈로그 아님).
        int a = HealthDamage(CombatHandler.BuildDamageMods(CombatHandler.ResolveAbility(2)!, 0, 0));
        int b = HealthDamage(CombatHandler.BuildDamageMods(CombatHandler.ResolveAbility(3)!, 0, 0));
        int c = HealthDamage(CombatHandler.BuildDamageMods(CombatHandler.ResolveAbility(4)!, 0, 0));

        Assert.Equal(10, a);
        Assert.Equal(15, b);
        Assert.Equal(25, c);
    }
}
