using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Monster;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Actors;

/// <summary>
/// 몬스터가 <b>자기 틱을 온전히 소유한다</b> — 이동·페이즈 결정, 어빌리티 선택, 쿨다운 커밋까지.
///
/// <para>예전에는 <c>Tick</c> 이 <c>int targetIdx</c> 만 돌려주고 어빌리티 선택·쿨다운 커밋은
/// 방(RoomSimulation)이 다시 판단했다 — 결정이 두 곳에 쪼개져 있었다.
/// 이제 방은 결과(<see cref="ActorTickResult"/>)를 <b>패킷·피해로 번역</b>만 한다.</para>
///
/// <para>여기서 고정하는 경계: <b>Tick 은 패킷도 피해도 만들지 않는다.</b>
/// 대상의 방어력을 읽어야 하는 데미지 산정이 액터로 들어오면 액터가 다른 액터를 뒤지기 시작한다.</para>
/// </summary>
public class MonsterActorTickTests
{
    private static readonly MapBounds Bounds = new(0f, 0f, 40f, 40f);

    private static MonsterActor NewDemon()
    {
        var m = new MonsterActor(1) { MonsterId = "creepy_demon", Phase = MonsterPhase.Idle };
        m.Gas.DefineResource(EGameplayAttribute.Health, 40);
        return m;
    }

    private static IReadOnlyList<TargetPos> At(float x, float z) => new[] { new TargetPos(x, z) };

    [Fact]
    public void 타깃이_없으면_Idle이고_아무것도_발동하지_않는다()
    {
        var m = NewDemon();

        var result = m.Tick(0.1f, 1_000_000, Array.Empty<TargetPos>(), Bounds);

        Assert.Equal(MonsterPhase.Idle, m.Phase);
        Assert.Equal(-1, result.TargetIndex);
        Assert.Null(result.Cast);
    }

    [Fact]
    public void aggro_밖_타깃은_노리지_않는다()
    {
        var m = NewDemon(); // creepy_demon aggro 7

        var result = m.Tick(0.1f, 1_000_000, At(20f, 0f), Bounds);

        Assert.Equal(-1, result.TargetIndex);
        Assert.Null(result.Cast);
    }

    [Fact]
    public void aggro_안_사거리_밖이면_추격만_하고_발동하지_않는다()
    {
        var m = NewDemon(); // aggro 7 / 사거리 1.3

        var result = m.Tick(0.1f, 1_000_000, At(5f, 0f), Bounds);

        Assert.Equal(MonsterPhase.Chase, m.Phase);
        Assert.Equal(0, result.TargetIndex); // 노리고는 있다
        Assert.Null(result.Cast);            // 그러나 아직 못 때린다
    }

    [Fact]
    public void 사거리_안이면_어빌리티를_고르고_쿨다운을_스스로_커밋한다()
    {
        var m = NewDemon();

        var result = m.Tick(0.1f, 1_000_000, At(0.5f, 0f), Bounds);

        Assert.Equal(MonsterPhase.Attack, m.Phase);
        Assert.Equal(0, result.TargetIndex);
        Assert.NotNull(result.Cast);
        Assert.Equal(1_000_000, m.Gas.LastCast(result.Cast!.Id)); // 커밋이 액터 안에서 끝났다
    }

    [Fact]
    public void 쿨다운_중에는_다시_고르지_않는다()
    {
        var m = NewDemon();
        var first = m.Tick(0.1f, 1_000_000, At(0.5f, 0f), Bounds);
        Assert.NotNull(first.Cast);

        var second = m.Tick(0.1f, 1_000_100, At(0.5f, 0f), Bounds); // 100ms 뒤

        Assert.Equal(0, second.TargetIndex); // 여전히 노리지만
        Assert.Null(second.Cast);            // 발동은 안 한다
    }

    [Fact]
    public void 쿨다운이_지나면_다시_발동한다()
    {
        var m = NewDemon();
        m.Tick(0.1f, 1_000_000, At(0.5f, 0f), Bounds);

        var again = m.Tick(0.1f, 1_002_000, At(0.5f, 0f), Bounds); // creepy_demon 쿨다운 1400ms

        Assert.NotNull(again.Cast);
    }

    [Fact]
    public void 스턴_태그가_붙으면_발동이_막힌다()
    {
        // 발동 게이트가 액터의 태그를 실제로 읽는다는 증거(예전엔 blocked:false 하드코딩).
        var m = NewDemon();
        m.Gas.AddTag(GameplayTags.Stun);

        var result = m.Tick(0.1f, 1_000_000, At(0.5f, 0f), Bounds);

        Assert.Equal(0, result.TargetIndex); // 노리기는 한다
        Assert.Null(result.Cast);            // 그러나 못 쓴다
        Assert.Equal(0, m.Gas.LastCast("creepy_demon_attack")); // 쿨다운도 소모되지 않는다
    }

    [Fact]
    public void 사망_태그가_붙으면_발동이_막힌다()
    {
        var m = NewDemon();
        m.Gas.AddTag(GameplayTags.Dead);

        Assert.Null(m.Tick(0.1f, 1_000_000, At(0.5f, 0f), Bounds).Cast);
    }

    [Fact]
    public void 플레이어_액터는_틱에서_아무_결정도_하지_않는다()
    {
        // 플레이어는 클라 권위 이동이라 서버가 움직이지 않는다. 마나 회복만.
        var p = new PlayerActor(100);
        p.Gas.DefineResource(EGameplayAttribute.Mana, 100);
        p.Gas[EGameplayAttribute.Mana] = 0;

        var result = p.Tick(1.0f, 1_000_000, At(0f, 0f), Bounds);

        Assert.Equal(ActorTickResult.None, result);
        Assert.True(p.Gas[EGameplayAttribute.Mana] > 0);
    }
}
