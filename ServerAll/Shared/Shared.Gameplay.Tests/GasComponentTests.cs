using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// GasComponent — 속성 적용이 <b>Health 전용이 아니라 속성 일반</b>이 됐다는 것을 고정한다.
/// 예전 구현은 <c>mods.Where(m =&gt; m.AttributeType == Health)</c> 로 Health 만 걸러서,
/// 카탈로그에 이미 있던 <c>atk_up_20</c>(AttackPower)·<c>def_down_10</c>(Defense)을 **서버가 적용할 수 없었다**.
/// </summary>
public class GasComponentTests
{
    private static GasComponent Player(int hp = 100, int mana = 50, int atk = 25, int def = 8)
    {
        var gas = new GasComponent();
        gas.DefineResource(EGameplayAttribute.Health, hp);
        gas.DefineResource(EGameplayAttribute.Mana, mana);
        gas.DefineStat(EGameplayAttribute.AttackPower, atk);
        gas.DefineStat(EGameplayAttribute.Defense, def);
        return gas;
    }

    /// <summary>몬스터 — Health 만 보유(공격력·방어력·마나 미보유).</summary>
    private static GasComponent Monster(int hp = 30)
    {
        var gas = new GasComponent();
        gas.DefineResource(EGameplayAttribute.Health, hp);
        return gas;
    }

    private static GameplayAttributeModifier Add(EGameplayAttribute a, int amount)
        => GameplayAttributeModifier.Create(a, amount, EModifierType.Additive);

    [Fact]
    public void Health_모디파이어는_HP를_깎고_0에서_사망이다()
    {
        var gas = Player();

        gas.ApplyModifiers(new[] { Add(EGameplayAttribute.Health, -30) });
        Assert.Equal(70, gas[EGameplayAttribute.Health]);
        Assert.False(gas.IsDead);

        gas.ApplyModifiers(new[] { Add(EGameplayAttribute.Health, -999) });
        Assert.Equal(0, gas[EGameplayAttribute.Health]);
        Assert.True(gas.IsDead);
    }

    [Fact]
    public void Health_이외_속성도_적용된다_Defense()
    {
        // def_down_10 과 같은 형태. 예전엔 Health 필터에 걸려 조용히 버려졌다.
        var gas = Player(def: 8);

        gas.ApplyModifiers(new[] { Add(EGameplayAttribute.Defense, -10) });

        Assert.Equal(0, gas[EGameplayAttribute.Defense]); // 8 - 10 → 하한 0
    }

    [Fact]
    public void Health_이외_속성도_적용된다_AttackPower_곱연산()
    {
        // atk_up_20 과 같은 형태(Multiplicative 120 = ×1.2). 스탯은 상한이 없어 base 를 넘을 수 있다.
        var gas = Player(atk: 25);

        gas.ApplyModifiers(new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 120, EModifierType.Multiplicative),
        });

        Assert.Equal(30, gas[EGameplayAttribute.AttackPower]); // 25 × 1.2
    }

    [Fact]
    public void 미보유_속성_모디파이어는_무시되고_속성이_생기지도_않는다()
    {
        // 몬스터에 마나가 몰래 생기면 발동 게이트가 엉뚱한 값을 보게 된다.
        var gas = Monster();

        gas.ApplyModifiers(new[] { Add(EGameplayAttribute.Mana, 50) });

        Assert.False(gas.Has(EGameplayAttribute.Mana));
        Assert.Equal(0, gas[EGameplayAttribute.Mana]);
    }

    [Fact]
    public void 몬스터는_공격력_방어력이_0이_아니라_미보유다()
    {
        var gas = Monster();

        Assert.False(gas.Has(EGameplayAttribute.AttackPower));
        Assert.False(gas.Has(EGameplayAttribute.Defense));
        Assert.Equal(0, gas[EGameplayAttribute.AttackPower]); // 읽기 편의상 0 으로 보이지만 "없음"이다
        Assert.Equal(0, gas[EGameplayAttribute.Defense]);
    }

    [Fact]
    public void 한_번에_여러_속성을_적용한다()
    {
        var gas = Player(hp: 100, def: 8);

        gas.ApplyModifiers(new[]
        {
            Add(EGameplayAttribute.Health, -25),
            Add(EGameplayAttribute.Defense, -3),
        });

        Assert.Equal(75, gas[EGameplayAttribute.Health]);
        Assert.Equal(5, gas[EGameplayAttribute.Defense]);
    }

    [Fact]
    public void 같은_속성의_모디파이어는_한_번에_집계된다()
    {
        // 순차 적용이 아니라 Aggregate 규칙(Σ Additive 후 × Multiplicative)이 성립해야 한다.
        var gas = Player(hp: 100);

        gas.ApplyModifiers(new[]
        {
            Add(EGameplayAttribute.Health, -10),
            Add(EGameplayAttribute.Health, -20),
        });

        Assert.Equal(70, gas[EGameplayAttribute.Health]);
    }

    [Fact]
    public void 마나_차감과_회복은_상한과_하한을_지킨다()
    {
        var gas = Player(mana: 100);
        gas[EGameplayAttribute.Mana] = 100;

        Assert.True(gas.TrySpendMana(30));
        Assert.Equal(70, gas[EGameplayAttribute.Mana]);
        Assert.False(gas.TrySpendMana(999)); // 부족 → 변경 없음
        Assert.Equal(70, gas[EGameplayAttribute.Mana]);

        gas.RegenMana(10f); // 충분히 큰 dt → 상한 클램프
        Assert.Equal(100, gas[EGameplayAttribute.Mana]);
    }

    [Fact]
    public void 마나가_미보유면_회복도_차감도_상태를_만들지_않는다()
    {
        var gas = Monster();

        gas.RegenMana(10f);
        Assert.False(gas.Has(EGameplayAttribute.Mana));

        Assert.True(gas.TrySpendMana(0));   // 무료는 통과
        Assert.False(gas.TrySpendMana(10)); // 없으니 못 낸다
    }

    [Fact]
    public void 발동은_사망_스턴_태그로_차단된다()
    {
        var gas = Player();
        Assert.False(gas.IsActivationBlocked);

        gas.AddTag(GameplayTags.Stun);
        Assert.True(gas.IsActivationBlocked);

        gas.RemoveTag(GameplayTags.Stun);
        gas.AddTag(GameplayTags.Dead);
        Assert.True(gas.IsActivationBlocked);
    }

    [Fact]
    public void 쿨다운은_어빌리티별로_독립_추적된다()
    {
        var gas = Player();

        Assert.True(gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1000));
        Assert.False(gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1399));
        Assert.True(gas.TryBeginAbility("other", cooldownMs: 400, nowMs: 1399)); // 다른 어빌리티는 독립
        Assert.True(gas.TryBeginAbility("swing", cooldownMs: 400, nowMs: 1400));
    }
}
