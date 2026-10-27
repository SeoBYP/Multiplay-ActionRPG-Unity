using Script.System.GamePlayAbilitySystem;

namespace Shared.Gameplay.Tests;

/// <summary>
/// 활성 Effect — <b>지속효과의 만료를 서버가 소유한다</b>는 것을 고정한다.
///
/// <para>서버가 만료를 소유하지 않으면 <c>IsActivationBlocked</c> 가 스턴 중에도 false 라
/// 스턴을 무시하는 클라를 막을 수 없다.</para>
/// </summary>
public class AbilitySystemComponentActiveEffectTests
{
    private static AbilitySystemComponent Player(int hp = 100, int mana = 50, int atk = 25, int def = 20)
    {
        var gas = new AbilitySystemComponent();
        gas.DefineResource(EGameplayAttribute.Health, hp);
        gas.DefineResource(EGameplayAttribute.Mana, mana);
        gas.DefineStat(EGameplayAttribute.AttackPower, atk);
        gas.DefineStat(EGameplayAttribute.Defense, def);
        return gas;
    }

    private static GameplayEffectDefinition Stun(int durationMs = 1500) => new(
        id: "test_stun", category: EEffectCategory.Defense, policy: EDurationPolicy.Duration,
        durationMs: durationMs, modifiers: new List<GameplayAttributeModifier>(),
        stack: EStackPolicy.Refresh, grantedTags: new GameplayTag[] { GameplayTags.Stun });

    private static GameplayEffectDefinition AtkUp(int amount = 10, int durationMs = 5000,
        EStackPolicy stack = EStackPolicy.None, int maxStacks = 1) => new(
        id: "test_atk_up", category: EEffectCategory.AttackPower, policy: EDurationPolicy.Duration,
        durationMs: durationMs,
        modifiers: new[] { GameplayAttributeModifier.Create(EGameplayAttribute.AttackPower, amount, EModifierType.Additive) },
        stack: stack, maxStacks: maxStacks);

    // ── 태그 · 발동 차단 ────────────────────────────────────────────

    [Fact]
    public void 스턴효과를_적용하면_Stun_태그가_붙고_발동이_차단된다()
    {
        var gas = Player();
        Assert.False(gas.IsActivationBlocked);

        gas.ApplyEffect(Stun(), instanceId: 1, nowMs: 1000);

        Assert.True(gas.HasTag(GameplayTags.Stun));
        Assert.True(gas.IsActivationBlocked);
    }

    [Fact]
    public void 지속시간이_지나면_만료되어_스턴_태그가_풀린다()
    {
        var gas = Player();
        gas.ApplyEffect(Stun(durationMs: 1500), instanceId: 1, nowMs: 1000);

        Assert.Null(gas.TickEffects(2499)); // 아직 만료 전 — 할당조차 하지 않는다
        Assert.True(gas.IsActivationBlocked);

        var expired = gas.TickEffects(2500);

        Assert.NotNull(expired);
        Assert.Equal(new[] { 1 }, expired);
        Assert.False(gas.HasTag(GameplayTags.Stun));
        Assert.False(gas.IsActivationBlocked);
        Assert.Equal(0, gas.ActiveEffectCount);
    }

    [Fact]
    public void 직접부여_태그는_효과_만료에_영향받지_않는다()
    {
        var gas = Player();
        gas.AddTag(GameplayTags.Dead);
        gas.ApplyEffect(Stun(), instanceId: 1, nowMs: 0);

        gas.TickEffects(99999);

        Assert.True(gas.HasTag(GameplayTags.Dead)); // 사망은 Effect 가 아니라 직접 태그다
        Assert.False(gas.HasTag(GameplayTags.Stun));
    }

    [Fact]
    public void 같은_태그를_주는_효과가_둘이면_하나가_끝나도_태그가_유지된다()
    {
        var gas = Player();
        var shortStun = Stun(durationMs: 1000);
        var longStun = new GameplayEffectDefinition(
            id: "test_stun_long", category: EEffectCategory.Defense, policy: EDurationPolicy.Duration,
            durationMs: 5000, modifiers: new List<GameplayAttributeModifier>(),
            grantedTags: new GameplayTag[] { GameplayTags.Stun });

        gas.ApplyEffect(shortStun, instanceId: 1, nowMs: 0);
        gas.ApplyEffect(longStun, instanceId: 2, nowMs: 0);

        gas.TickEffects(1000); // 짧은 쪽만 만료

        Assert.Equal(1, gas.ActiveEffectCount);
        Assert.True(gas.HasTag(GameplayTags.Stun)); // 회수 장부를 두지 않는 이유가 이것
    }

    // ── 만료 반환 계약 ──────────────────────────────────────────────

    [Fact]
    public void 활성효과가_없으면_틱은_null을_돌려준다()
    {
        Assert.Null(Player().TickEffects(12345)); // 매 틱 빈 리스트를 할당하지 않는다
    }

    [Fact]
    public void 무한지속_효과는_만료되지_않는다()
    {
        var gas = Player();
        var aura = new GameplayEffectDefinition(
            id: "test_aura", category: EEffectCategory.AttackPower, policy: EDurationPolicy.Infinite,
            durationMs: 0, modifiers: new List<GameplayAttributeModifier>(),
            grantedTags: new GameplayTag[] { GameplayTags.Stun });

        gas.ApplyEffect(aura, instanceId: 1, nowMs: 0);

        Assert.Null(gas.TickEffects(long.MaxValue / 2));
        Assert.True(gas.HasTag(GameplayTags.Stun));
    }

    // ── 적용 정책 ───────────────────────────────────────────────────

    [Fact]
    public void 즉발효과는_활성목록에_올라가지_않고_즉시_적용된다()
    {
        var gas = Player(hp: 100);
        var damage = new GameplayEffectDefinition(
            id: "test_dmg", category: EEffectCategory.AttackPower, policy: EDurationPolicy.Instant,
            durationMs: 0,
            modifiers: new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -30, EModifierType.Additive) });

        int id = gas.ApplyEffect(damage, instanceId: 7, nowMs: 0);

        Assert.Equal(-1, id); // 되돌릴 것이 없으므로 인스턴스가 남지 않는다
        Assert.Equal(0, gas.ActiveEffectCount);
        Assert.Equal(70, gas[EGameplayAttribute.Health]);
    }

    [Fact]
    public void 같은_인스턴스_재적용은_중복되지_않는다()
    {
        var gas = Player();
        gas.ApplyEffect(Stun(), instanceId: 1, nowMs: 0);
        gas.ApplyEffect(Stun(), instanceId: 1, nowMs: 500); // 중복 패킷 도착

        Assert.Equal(1, gas.ActiveEffectCount);
        Assert.Null(gas.TickEffects(1600)); // StartMs 가 500 으로 갱신됐다
        Assert.NotNull(gas.TickEffects(2000));
    }

    [Fact]
    public void Refresh_정책은_새_인스턴스를_만들지_않고_지속시간만_갱신한다()
    {
        var gas = Player();
        gas.ApplyEffect(Stun(durationMs: 1000), instanceId: 1, nowMs: 0);
        int reused = gas.ApplyEffect(Stun(durationMs: 1000), instanceId: 2, nowMs: 800);

        Assert.Equal(1, reused);            // 기존 인스턴스를 재사용
        Assert.Equal(1, gas.ActiveEffectCount);
        Assert.Null(gas.TickEffects(1500)); // 800 기준으로 갱신돼 아직 살아 있다
    }

    [Fact]
    public void RemoveEffect는_멱등이다()
    {
        var gas = Player();
        gas.ApplyEffect(Stun(), instanceId: 1, nowMs: 0);

        Assert.True(gas.RemoveEffect(1));
        Assert.False(gas.RemoveEffect(1)); // 두 번째는 아무 일도 없다
        Assert.False(gas.HasTag(GameplayTags.Stun));
    }

    // ── 스탯 재계산 (Base 기준) ─────────────────────────────────────

    [Fact]
    public void 지속_스탯버프는_적용되고_만료되면_원래_값으로_돌아온다()
    {
        var gas = Player(atk: 25);

        gas.ApplyEffect(AtkUp(amount: 10), instanceId: 1, nowMs: 0);
        Assert.Equal(35, gas[EGameplayAttribute.AttackPower]);

        gas.TickEffects(5000);
        Assert.Equal(25, gas[EGameplayAttribute.AttackPower]); // Base 로 복귀 — 영구 버프가 되지 않는다
    }

    [Fact]
    public void 스택_정책은_모디파이어를_중첩_적용한다()
    {
        var gas = Player(atk: 25);
        var def = AtkUp(amount: 10, stack: EStackPolicy.Stack, maxStacks: 3);

        gas.ApplyEffect(def, instanceId: 1, nowMs: 0);
        gas.ApplyEffect(def, instanceId: 2, nowMs: 100);
        gas.ApplyEffect(def, instanceId: 3, nowMs: 200);
        gas.ApplyEffect(def, instanceId: 4, nowMs: 300); // MaxStacks 초과 — 무시된다

        Assert.Equal(1, gas.ActiveEffectCount);
        Assert.Equal(55, gas[EGameplayAttribute.AttackPower]); // 25 + 10×3
    }

    [Fact]
    public void 자원은_재계산_대상이_아니다()
    {
        var gas = Player(hp: 100);
        gas.ApplyModifiers(new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -40, EModifierType.Additive),
        });

        gas.ApplyEffect(AtkUp(), instanceId: 1, nowMs: 0);
        gas.TickEffects(5000);

        // 스탯 재계산이 HP 까지 Base 로 되돌리면 피해가 사라진다(= 불사).
        Assert.Equal(60, gas[EGameplayAttribute.Health]);
    }

    [Fact]
    public void 카탈로그의_stun_1_5s_는_서버에서도_Stun_태그를_부여한다()
    {
        var gas = Player();
        var def = new GameplayEffectCatalog().Get("stun_1_5s");

        Assert.NotNull(def);
        gas.ApplyEffect(def, instanceId: 1, nowMs: 0);

        Assert.True(gas.IsActivationBlocked);
        Assert.NotNull(gas.TickEffects(1500)); // durationMs 1500
    }

    // ── 스레드 안전 ─────────────────────────────────────────────────

    [Fact]
    public void 동시_적용과_만료가_섞여도_활성목록이_깨지지_않는다()
    {
        var gas = Player();

        Parallel.For(0, 200, i =>
        {
            gas.ApplyEffect(AtkUp(durationMs: 10), instanceId: i, nowMs: 0);
            gas.TickEffects(50);
            _ = gas.HasTag(GameplayTags.Stun);
            _ = gas[EGameplayAttribute.AttackPower];
        });

        gas.TickEffects(1000);
        Assert.Equal(0, gas.ActiveEffectCount);
        Assert.Equal(25, gas[EGameplayAttribute.AttackPower]);
    }

    [Fact]
    public void None_정책은_재적용을_무시한다()
    {
        // 클라 EffectSystemTests 의 의도를 Shared 가 그대로 따른다.
        // 여기가 갈리면 같은 버프를 두 번 건 뒤 클라는 1개, 서버는 2개가 된다.
        var gas = Player(atk: 25);
        var once = AtkUp(amount: 10, stack: EStackPolicy.None);

        int first = gas.ApplyEffect(once, instanceId: 1, nowMs: 0);
        int second = gas.ApplyEffect(once, instanceId: 2, nowMs: 500);

        Assert.Equal(first, second);           // 기존 인스턴스 id 를 그대로 돌려준다
        Assert.Equal(1, gas.ActiveEffectCount);
        Assert.Equal(35, gas[EGameplayAttribute.AttackPower]); // 중첩되지 않는다
        Assert.NotNull(gas.TickEffects(5000)); // 지속시간도 갱신되지 않는다(0 기준 5000ms)
    }

    // ── 값 타입 계약 (할당 결정을 못박는다) ────────────────────────

    [Fact]
    public void ActiveGameplayEffect_는_값_타입이다()
    {
        // class 로 되돌리면 피격마다·만료마다 힙 객체가 하나씩 생긴다.
        // 이 타입은 필드 넷뿐이라 List 배열에 인라인으로 들어가는 편이 맞다.
        Assert.True(typeof(ActiveGameplayEffect).IsValueType);
    }

    [Fact]
    public void 스택_갱신은_목록에_되쓰여야_한다()
    {
        // struct 조회는 복사본을 준다. 되쓰기를 빠뜨리면 갱신이 조용히 사라진다 —
        // 이 테스트가 그 회귀의 감지기다(내부 목록에서 다시 읽어 확인한다).
        var gas = Player(atk: 25);
        var def = AtkUp(amount: 10, durationMs: 1000, stack: EStackPolicy.Stack, maxStacks: 3);

        gas.ApplyEffect(def, instanceId: 1, nowMs: 0);
        gas.ApplyEffect(def, instanceId: 2, nowMs: 800);

        var active = Assert.Single(gas.ActiveEffects());
        Assert.Equal(2, active.Stacks);     // AddStack 이 반영됐다
        Assert.Equal(800, active.StartMs);  // Refresh 도 반영됐다
        Assert.Null(gas.TickEffects(1500)); // 800 기준이라 아직 살아 있다
    }

    [Fact]
    public void 스냅샷을_들고_있어도_내부_갱신에_영향받지_않는다()
    {
        // 값 타입이라 스냅샷은 그 시점의 복사본이다. 참조를 흘리면 밖에서 내부 상태를 흔들 수 있다.
        var gas = Player();
        gas.ApplyEffect(Stun(durationMs: 1000), instanceId: 1, nowMs: 0);
        var snapshot = gas.ActiveEffects()[0];

        gas.ApplyEffect(Stun(durationMs: 1000), instanceId: 1, nowMs: 500); // 내부 갱신

        Assert.Equal(0, snapshot.StartMs);                    // 스냅샷은 그대로
        Assert.Equal(500, gas.ActiveEffects()[0].StartMs);    // 내부는 갱신됨
    }
}
