using System.Linq;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// GAS 태그 인프라(ⓒ) — GameplayTag 값 동등성 + GameplayTagContainer 집합 동작 + Effect.GrantedTags.
/// 상태 표현(예: State.Dead)·Cue 트리거의 토대. 정확 일치만(계층 매칭은 후속).
/// </summary>
public class GameplayTagTests
{
    [Fact]
    public void 같은_문자열_태그는_값으로_동등하다()
    {
        GameplayTag a = "State.Dead";
        GameplayTag b = new GameplayTag("State.Dead");

        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void 다른_문자열_태그는_동등하지_않다()
    {
        Assert.True((GameplayTag)"State.Dead" != (GameplayTag)"State.Stunned");
    }

    [Fact]
    public void 빈_태그와_default는_무효하다()
    {
        Assert.False(default(GameplayTag).IsValid);
        Assert.False(((GameplayTag)"").IsValid);
        Assert.True(((GameplayTag)"State.Dead").IsValid);
        Assert.Equal(string.Empty, default(GameplayTag).Value);
    }

    [Fact]
    public void 컨테이너는_태그_추가_보유_제거한다()
    {
        var c = new GameplayTagContainer();

        Assert.True(c.Add("State.Dead"));
        Assert.True(c.HasTag("State.Dead"));
        Assert.Equal(1, c.Count);

        Assert.True(c.Remove("State.Dead"));
        Assert.False(c.HasTag("State.Dead"));
        Assert.Equal(0, c.Count);
    }

    [Fact]
    public void 컨테이너는_중복과_무효_태그를_거른다()
    {
        var c = new GameplayTagContainer();

        Assert.True(c.Add("State.Dead"));
        Assert.False(c.Add("State.Dead")); // 중복
        Assert.False(c.Add(""));           // 무효
        Assert.Equal(1, c.Count);
    }

    [Fact]
    public void HasAny는_하나라도_보유하면_참이다()
    {
        var c = new GameplayTagContainer();
        c.Add("State.Buff.Atk");

        Assert.True(c.HasAny(new GameplayTag[] { "State.Dead", "State.Buff.Atk" }));
        Assert.False(c.HasAny(new GameplayTag[] { "State.Dead", "State.Stunned" }));
    }

    [Fact]
    public void Effect_GrantedTags는_기본_빈목록이고_지정시_보존된다()
    {
        var noTags = new GameplayEffectDefinition(
            "atk_up", EEffectCategory.AttackPower, EDurationPolicy.Duration, 1000,
            new[] { GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, 110, EModifierType.Multiplicative) });
        Assert.Empty(noTags.GrantedTags);

        var stun = new GameplayEffectDefinition(
            "stun", EEffectCategory.MoveSpeed, EDurationPolicy.Duration, 2000,
            new[] { GameplayAttributeModifier.Create(EGameplayAttribute.MoveSpeed, 0, EModifierType.Multiplicative) },
            grantedTags: new GameplayTag[] { "State.Stunned" });

        var tag = Assert.Single(stun.GrantedTags);
        Assert.Equal("State.Stunned", tag.Value);
    }
}
