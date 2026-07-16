using Server.PacketHandler.Handler;
using Server.Player;

namespace Server.Tests.Combat;

/// <summary>
/// #7 콤보 서버 권위 cadence — <b>타이밍 진실원 = 어빌리티 데이터</b>(SkillTimeline.ComboChainMs, abilities.json — AC-B).
///
/// 콤보는 단계마다 skillId 가 다르다(2=combo_a·3=combo_b·4=combo_c). 따라서 **단계별 개별 쿨다운만으로는
/// A→B→C 즉시 3연타를 못 막는다**(각자 첫 발동이라 쿨다운이 비어 있음) → 합산 폭딜(10+15+25) 치팅.
/// 그래서 서버는 직전 콤보 스윙의 ComboChainMs 가 지나기 전의 다음 콤보 공격을 거부한다.
/// 클라 ComboDriver 가 쓰는 값과 동일하므로 서버 권위와 애니가 어긋나지 않는다.
/// </summary>
public class ComboCadenceTests
{
    [Fact]
    public void IsComboSkill_은_2_3_4만_콤보로_본다()
    {
        Assert.True(CombatHandler.IsComboSkill(2));
        Assert.True(CombatHandler.IsComboSkill(3));
        Assert.True(CombatHandler.IsComboSkill(4));

        Assert.False(CombatHandler.IsComboSkill(0)); // basic_swing
        Assert.False(CombatHandler.IsComboSkill(1)); // heavy_swing
    }

    [Fact]
    public void 콤보_스킬_데이터에_체인_타이밍이_저작돼_있다()
    {
        // 진실원 확인 — 서버가 이 값으로 cadence 를 강제한다. 불변식: chain ≤ window.
        foreach (var id in new[] { "combo_a", "combo_b", "combo_c" })
        {
            var skill = Shared.Infrastructure.Abilities.AbilityCatalog.Get(id)?.Timeline;
            Assert.NotNull(skill);
            Assert.True(skill!.ComboChainMs > 0, $"{id}: ComboChainMs 가 저작돼야 한다");
            Assert.True(skill.ComboChainMs <= skill.ComboWindowMs, $"{id}: chain({skill.ComboChainMs}) ≤ window({skill.ComboWindowMs})");
        }

        // 비콤보 스킬은 0(게이트 없음).
        Assert.Equal(0, Shared.Infrastructure.Abilities.AbilityCatalog.Get("basic_swing")!.Timeline.ComboChainMs);
    }

    [Fact]
    public void 직전_단계의_ComboChainMs_전에는_다음_콤보를_거부한다()
    {
        var state = new PlayerState { UserId = 1 };
        var comboA = Shared.Infrastructure.Abilities.AbilityCatalog.Get("combo_a")!.Timeline;
        long t = 10_000;

        // A 발동 — 첫 콤보라 통과. 이후 A 의 ComboChainMs 만큼 다음 콤보가 막힌다.
        Assert.True(state.TryBeginComboAttack(t, comboA.ComboChainMs, CombatHandler.ComboMinIntervalMs));

        // 즉시 B 시도(연타) — A 의 체인 지점 전이라 거부. 개별 쿨다운이 비어 있어도 여기서 막힌다.
        Assert.False(state.TryBeginComboAttack(t + 1, 0, CombatHandler.ComboMinIntervalMs));
        Assert.False(state.TryBeginComboAttack(t + comboA.ComboChainMs - 1, 0, CombatHandler.ComboMinIntervalMs));

        // A 의 체인 지점이 지나면 B 허용.
        var comboB = Shared.Infrastructure.Abilities.AbilityCatalog.Get("combo_b")!.Timeline;
        Assert.True(state.TryBeginComboAttack(t + comboA.ComboChainMs, comboB.ComboChainMs, CombatHandler.ComboMinIntervalMs));
    }

    [Fact]
    public void 네트워크_지터_허용치만큼은_일찍_도착해도_받아준다()
    {
        // 클라는 정확히 ComboChainMs 간격으로 보내지만 패킷별 지연 차로 서버 도착 간격이 더 짧아질 수 있다.
        // 허용치가 없으면 **정상 콤보가 거부돼 데미지가 유실**된다(던전에서만 나는 버그).
        var state = new PlayerState { UserId = 1 };
        var comboA = Shared.Infrastructure.Abilities.AbilityCatalog.Get("combo_a")!.Timeline;
        long t = 10_000;

        Assert.True(state.TryBeginComboAttack(t, comboA.ComboChainMs, CombatHandler.ComboMinIntervalMs, CombatHandler.ComboCadenceToleranceMs));

        // 허용치 안쪽으로 일찍 도착 → 받아준다.
        long slightlyEarly = t + comboA.ComboChainMs - CombatHandler.ComboCadenceToleranceMs;
        Assert.True(state.TryBeginComboAttack(slightlyEarly, 0, CombatHandler.ComboMinIntervalMs, CombatHandler.ComboCadenceToleranceMs),
            "지터 허용치 안쪽의 조기 도착은 받아줘야 한다(정상 콤보 유실 방지)");
    }

    [Fact]
    public void 허용치를_넘는_연타는_여전히_거부한다()
    {
        // 허용치를 줘도 버스트(즉시 3연타) 차단은 유지돼야 한다.
        var state = new PlayerState { UserId = 1 };
        var comboA = Shared.Infrastructure.Abilities.AbilityCatalog.Get("combo_a")!.Timeline;
        long t = 10_000;

        Assert.True(state.TryBeginComboAttack(t, comboA.ComboChainMs, CombatHandler.ComboMinIntervalMs, CombatHandler.ComboCadenceToleranceMs));

        Assert.False(state.TryBeginComboAttack(t + 1, 0, CombatHandler.ComboMinIntervalMs, CombatHandler.ComboCadenceToleranceMs),
            "즉시 연타는 허용치를 줘도 거부");
        Assert.False(state.TryBeginComboAttack(t + comboA.ComboChainMs - CombatHandler.ComboCadenceToleranceMs - 1, 0,
                CombatHandler.ComboMinIntervalMs, CombatHandler.ComboCadenceToleranceMs),
            "허용치를 1ms 넘겨 빠른 것도 거부");
    }

    [Fact]
    public void 데이터가_0이면_최소_안전값으로_폴백한다()
    {
        // 저작 누락(chainMs=0)이어도 버스트 구멍이 열리면 안 된다 → ComboMinIntervalMs 로 폴백.
        var state = new PlayerState { UserId = 1 };
        long t = 10_000;

        Assert.True(state.TryBeginComboAttack(t, 0, CombatHandler.ComboMinIntervalMs)); // chainMs=0 → 폴백 기록
        Assert.False(state.TryBeginComboAttack(t + CombatHandler.ComboMinIntervalMs - 1, 0, CombatHandler.ComboMinIntervalMs));
        Assert.True(state.TryBeginComboAttack(t + CombatHandler.ComboMinIntervalMs, 0, CombatHandler.ComboMinIntervalMs));
    }
}
