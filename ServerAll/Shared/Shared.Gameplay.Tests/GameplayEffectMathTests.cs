using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// Stat 집계 결정론 검증. 이 기대 벡터는 클라이언트 EditMode `EffectSystemTests`와 동일해야 한다(미러 drift 방지).
/// </summary>
public class GameplayEffectMathTests
{
    private const int Max = 100000;

    [Fact]
    public void Additive_modifier는_base에_더해진다()
    {
        int result = GameplayEffectMath.Aggregate(50, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive),
        }, Max);

        Assert.Equal(60, result);
    }

    [Fact]
    public void Multiplicative_120퍼센트는_base를_1점2배한다()
    {
        int result = GameplayEffectMath.Aggregate(50, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 120, EModifierType.Multiplicative),
        }, Max);

        Assert.Equal(60, result);
    }

    [Fact]
    public void 같은_Additive를_3번_넣으면_합산된다()
    {
        int result = GameplayEffectMath.Aggregate(50, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive),
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive),
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive),
        }, Max);

        Assert.Equal(80, result);
    }

    [Fact]
    public void Additive_먼저_더한_뒤_Multiplicative를_곱한다()
    {
        int result = GameplayEffectMath.Aggregate(50, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 10, EModifierType.Additive),     // 60
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 200, EModifierType.Multiplicative), // ×2
        }, Max);

        Assert.Equal(120, result);
    }

    [Fact]
    public void 결과는_0과_Max로_clamp된다()
    {
        int over = GameplayEffectMath.Aggregate(50, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 1000, EModifierType.Additive),
        }, 100);
        Assert.Equal(100, over);

        int under = GameplayEffectMath.Aggregate(10, new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, -50, EModifierType.Additive),
        }, 100);
        Assert.Equal(0, under);
    }
}
