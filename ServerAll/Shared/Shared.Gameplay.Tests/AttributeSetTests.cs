using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// AttributeSet — <b>"없는 속성"과 "0 인 속성"의 구분</b>이 이 타입의 존재 이유다.
/// 필드로 두던 시절엔 그 구분이 불가능해 호출부가 리터럴 0 으로 위장했다
/// (<c>const int MonsterAttackPower = 0</c> · <c>const int MonsterDefense = 0</c> · <c>currentMana: 0</c>).
/// </summary>
public class AttributeSetTests
{
    [Fact]
    public void 부여하지_않은_속성은_0이_아니라_없음이다()
    {
        var set = new AttributeSet();
        set.Define(EGameplayAttribute.Health, 100, 100);

        Assert.True(set.Has(EGameplayAttribute.Health));
        Assert.False(set.Has(EGameplayAttribute.Mana));
        Assert.False(set.TryGet(EGameplayAttribute.Mana, out int mana));
        Assert.Equal(0, mana);
        Assert.Equal(7, set.GetOr(EGameplayAttribute.Mana, fallback: 7)); // 폴백은 호출부가 정한다
    }

    [Fact]
    public void 값이_0인_속성은_보유한_것이다()
    {
        // "스탯 0" 과 "스탯 없음"이 다르다는 것이 이 설계의 요점.
        var set = new AttributeSet();
        set.Define(EGameplayAttribute.AttackPower, 0, AttributeSet.NoMax);

        Assert.True(set.Has(EGameplayAttribute.AttackPower));
        Assert.True(set.TryGet(EGameplayAttribute.AttackPower, out int ap));
        Assert.Equal(0, ap);
    }

    [Fact]
    public void 현재값은_0과_Max_사이로_클램프된다()
    {
        var set = new AttributeSet();
        set.Define(EGameplayAttribute.Health, current: 150, max: 100); // 부여 시점부터 클램프

        Assert.Equal(100, set.GetOr(EGameplayAttribute.Health));

        set.SetCurrent(EGameplayAttribute.Health, -50);
        Assert.Equal(0, set.GetOr(EGameplayAttribute.Health));

        set.SetCurrent(EGameplayAttribute.Health, 9999);
        Assert.Equal(100, set.GetOr(EGameplayAttribute.Health));
    }

    [Fact]
    public void 미보유_속성에_쓰기를_해도_속성이_생기지_않는다()
    {
        // 없는 속성이 몰래 생기면 "없음"이라는 정보가 조용히 사라진다.
        var set = new AttributeSet();

        set.SetCurrent(EGameplayAttribute.Mana, 50);
        set.SetMax(EGameplayAttribute.Mana, 100);

        Assert.False(set.Has(EGameplayAttribute.Mana));
    }

    [Fact]
    public void 스탯은_상한이_없어_버프가_base를_넘을_수_있다()
    {
        var set = new AttributeSet();
        set.Define(EGameplayAttribute.AttackPower, 25, AttributeSet.NoMax);

        set.SetCurrent(EGameplayAttribute.AttackPower, 999);

        Assert.Equal(999, set.GetOr(EGameplayAttribute.AttackPower));
    }

    [Fact]
    public void Max를_낮추면_현재값도_함께_재클램프된다()
    {
        var set = new AttributeSet();
        set.Define(EGameplayAttribute.Health, 100, 100);

        set.SetMax(EGameplayAttribute.Health, 40);

        Assert.Equal(40, set.MaxOr(EGameplayAttribute.Health));
        Assert.Equal(40, set.GetOr(EGameplayAttribute.Health));
    }
}
