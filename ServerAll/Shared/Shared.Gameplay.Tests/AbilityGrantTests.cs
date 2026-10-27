using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>보유 어빌리티 — 서버 C_Attack 발동 권한의 근거(S1).</summary>
public class AbilityGrantTests
{
    [Fact]
    public void 부여하지_않은_어빌리티는_보유하지_않는다()
    {
        var gas = new AbilitySystemComponent();

        Assert.False(gas.HasAbility("basic_swing"));
    }

    [Fact]
    public void 부여한_어빌리티만_보유한다()
    {
        var gas = new AbilitySystemComponent();

        gas.GrantAbility("basic_swing");

        Assert.True(gas.HasAbility("basic_swing"));
        Assert.False(gas.HasAbility("test_brute_attack"));
    }

    [Fact]
    public void 같은_어빌리티를_두_번_부여해도_보유_상태는_같다()
    {
        var gas = new AbilitySystemComponent();

        gas.GrantAbility("basic_swing");
        gas.GrantAbility("basic_swing");

        Assert.True(gas.HasAbility("basic_swing"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void 빈_id는_부여되지도_보유되지도_않는다(string? abilityId)
    {
        var gas = new AbilitySystemComponent();

        gas.GrantAbility(abilityId!);

        Assert.False(gas.HasAbility(abilityId!));
    }
}
