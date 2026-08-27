using System.Collections.Concurrent;
using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// GAS 상태의 <b>동시 접근</b> — 서버에서 이 상태는 두 스레드가 만진다:
/// 틱 루프(마나 회복·피해)와 패킷 핸들러(마나 차감·쿨다운·회피).
///
/// <para>예전에는 락이 방/저장소에 있어서 <b>저장소를 지나는 경로만</b> 안전했고,
/// 핸들러가 <c>Gas</c> 를 직접 만지는 경로(<c>TrySpendMana</c>·<c>TryBeginAbility</c>·회피)는 구멍이었다.
/// 이 테스트들이 그 구멍을 고정한다 — 락을 AbilitySystemComponent 밖으로 옮기면 여기서 깨진다.</para>
/// </summary>
public class AbilitySystemComponentConcurrencyTests
{
    private static AbilitySystemComponent Player(int hp = 100, int mana = 100)
    {
        var gas = new AbilitySystemComponent();
        gas.DefineResource(EGameplayAttribute.Health, hp);
        gas.DefineResource(EGameplayAttribute.Mana, mana);
        return gas;
    }

    [Fact]
    public void 동시_마나차감은_보유량을_넘지_않는다()
    {
        // 검사와 차감이 원자가 아니면 둘 이상이 함께 통과해 마나가 음수가 되거나 공짜 시전이 생긴다.
        var gas = Player(mana: 100);

        int succeeded = 0;
        Parallel.For(0, 200, _ =>
        {
            if (gas.TrySpendMana(10))
                Interlocked.Increment(ref succeeded);
        });

        Assert.Equal(10, succeeded); // 100 / 10 — 정확히 열 번만 성공해야 한다
        Assert.Equal(0, gas[EGameplayAttribute.Mana]);
    }

    [Fact]
    public void 회복과_차감이_동시에_와도_마나가_유실되거나_상한을_넘지_않는다()
    {
        // 틱(RegenMana)과 핸들러(TrySpendMana)가 같은 항목을 동시에 쓰던 실제 경합의 재현.
        var gas = Player(mana: 100);
        gas[EGameplayAttribute.Mana] = 50;

        Parallel.Invoke(
            () => { for (int i = 0; i < 500; i++) gas.RegenMana(0.1f); },
            () => { for (int i = 0; i < 500; i++) gas.TrySpendMana(1); },
            () => { for (int i = 0; i < 500; i++) _ = gas[EGameplayAttribute.Mana]; });

        int mana = gas[EGameplayAttribute.Mana];
        Assert.InRange(mana, 0, 100); // 음수·상한 초과가 없어야 한다
    }

    [Fact]
    public void 동시_발동요청은_쿨다운_안에서_하나만_통과한다()
    {
        // 판정과 기록이 원자가 아니면 연사 패킷 여러 개가 같은 쿨다운 창을 함께 통과한다(폭딜 치팅).
        var gas = Player();

        int passed = 0;
        Parallel.For(0, 200, _ =>
        {
            if (gas.TryBeginAbility("swing", cooldownMs: 1000, nowMs: 5_000))
                Interlocked.Increment(ref passed);
        });

        Assert.Equal(1, passed);
    }

    [Fact]
    public void 동시_태그_부여는_한_번만_새로_붙는다()
    {
        // 다운 dedup(S_PlayerDead 1회 발화)이 이 반환값에 걸려 있다.
        var gas = Player();

        int newlyAdded = 0;
        Parallel.For(0, 200, _ =>
        {
            if (gas.AddTag(GameplayTags.Dead))
                Interlocked.Increment(ref newlyAdded);
        });

        Assert.Equal(1, newlyAdded);
        Assert.True(gas.HasTag(GameplayTags.Dead));
    }

    [Fact]
    public void 동시_태그_제거는_한_번만_성공한다()
    {
        // 부활 멱등(중복 C_Revive 차단)이 이 반환값에 걸려 있다.
        var gas = Player();
        gas.AddTag(GameplayTags.Dead);

        int removed = 0;
        Parallel.For(0, 200, _ =>
        {
            if (gas.RemoveTag(GameplayTags.Dead))
                Interlocked.Increment(ref removed);
        });

        Assert.Equal(1, removed);
    }

    [Fact]
    public void 동시_피해적용에도_HP가_정확히_누적된다()
    {
        // 읽고-집계하고-쓰기가 원자가 아니면 lost update 로 피해가 사라진다(= 불사에 가까워진다).
        var gas = Player(hp: 1000);
        var one = new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -1, EModifierType.Additive) };

        Parallel.For(0, 400, _ => gas.ApplyModifiers(one));

        Assert.Equal(600, gas[EGameplayAttribute.Health]); // 1000 − 400, 한 대도 유실되면 안 된다
    }

    [Fact]
    public void 읽기와_쓰기가_섞여도_속성_구조가_깨지지_않는다()
    {
        // Dictionary 를 락 없이 동시에 만지면 예외/손상이 난다. 어떤 예외도 새어나오면 안 된다.
        var gas = Player();
        var errors = new ConcurrentBag<Exception>();

        Parallel.For(0, 8, worker =>
        {
            try
            {
                for (int i = 0; i < 2000; i++)
                {
                    if (worker % 2 == 0) gas[EGameplayAttribute.Mana] = i % 100;
                    else _ = gas.Max(EGameplayAttribute.Mana) + gas[EGameplayAttribute.Health] + (gas.IsDead ? 1 : 0);
                }
            }
            catch (Exception e) { errors.Add(e); }
        });

        Assert.Empty(errors);
    }
}
