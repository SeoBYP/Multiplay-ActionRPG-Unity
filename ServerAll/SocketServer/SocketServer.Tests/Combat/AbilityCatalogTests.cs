using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Abilities;

namespace Server.Tests.Combat;

/// <summary>
/// 임베디드 abilities.json(클라 AbilityDefinition SO 저작→bake) 로드·조회 검증.
/// </summary>
public class AbilityCatalogTests
{
    [Fact]
    public void 플레이어_스킬과_몬스터_공격이_한_카탈로그에_로드된다()
    {
        // 플레이어 스킬과 몬스터 공격이 **같은 카탈로그** 단일 저작.
        foreach (var id in new[] { "basic_swing", "heavy_swing", "combo_a", "combo_b", "combo_c", "combo_d" })
            Assert.NotNull(AbilityCatalog.Get(id));
        foreach (var id in new[] { "creepy_demon_attack", "arachnya_attack", "leviathan_attack" })
            Assert.NotNull(AbilityCatalog.Get(id));
    }

    [Fact]
    public void networkId는_플레이어와_몬스터가_충돌하지_않는다()
    {
        // 플레이어 0~4 / 몬스터 100+ — 같은 int 공간을 쓰므로 겹치면 서버가 엉뚱한 어빌리티를 발동한다.
        // (Exporter 가 저작 시점에 중복을 막지만, 대역 분리는 여기서 고정)
        Assert.Equal("basic_swing", AbilityCatalog.Get(0)!.Id);
        Assert.Equal("creepy_demon_attack", AbilityCatalog.Get(100)!.Id);
        Assert.All(AbilityCatalog.All, a => Assert.Equal(a, AbilityCatalog.Get(a.NetworkId)));
    }

    [Fact]
    public void networkId는_기존_ResolveSkill_매핑을_보존한다()
    {
        // 패킷 계약 보존: C_Attack/S_AbilityActivated 의 SkillId(int).
        Assert.Equal("basic_swing", AbilityCatalog.Get(0)!.Id);
        Assert.Equal("heavy_swing", AbilityCatalog.Get(1)!.Id);
        Assert.Equal("combo_a",     AbilityCatalog.Get(2)!.Id);
        Assert.Equal("combo_b",     AbilityCatalog.Get(3)!.Id);
        Assert.Equal("combo_c",     AbilityCatalog.Get(4)!.Id);
        Assert.Equal("combo_d",     AbilityCatalog.Get(5)!.Id);
    }

    [Fact]
    public void 게임플레이_수치가_현재_저작값으로_bake_돼_있다()
    {
        // bake 동기화 가드 — 서버 임베디드 abilities.json 이 클라 SO 저작값과 일치하는지(Export 누락 탐지).
        // ⚠️ 밸런스를 조정하면 Export 후 여기 기대값도 함께 갱신한다.
        var basic = AbilityCatalog.Get("basic_swing")!;
        Assert.Equal(167, basic.Timeline.StartupMs);
        Assert.Equal(125, basic.Timeline.ActiveMs);
        Assert.Equal(150, basic.Timeline.RecoveryMs);
        Assert.Equal(400, basic.Timeline.CooldownMs);
        Assert.Equal(0, basic.Timeline.ManaCost);
        Assert.Equal(EHitboxShape.Box, basic.Timeline.Hitbox.Shape);

        var heavy = AbilityCatalog.Get("heavy_swing")!;
        Assert.Equal(400, heavy.Timeline.StartupMs);
        Assert.Equal(1200, heavy.Timeline.CooldownMs);
        Assert.Equal(20, heavy.Timeline.ManaCost);
    }

    [Fact]
    public void 콤보_리치가_단계별로_상승한다()
    {
        // 콤보 A<B<C 정면 리치 불변식.
        var a = AbilityCatalog.Get("combo_a")!;
        var b = AbilityCatalog.Get("combo_b")!;
        var c = AbilityCatalog.Get("combo_c")!;
        var d = AbilityCatalog.Get("combo_d")!;

        Assert.True(a.Timeline.Hitbox.HalfExtents.Z < b.Timeline.Hitbox.HalfExtents.Z, "combo_a 리치 < combo_b");
        Assert.True(b.Timeline.Hitbox.HalfExtents.Z < c.Timeline.Hitbox.HalfExtents.Z, "combo_b 리치 < combo_c");
        Assert.True(c.Timeline.Hitbox.HalfExtents.Z < d.Timeline.Hitbox.HalfExtents.Z, "combo_c 리치 < combo_d");
    }

    [Fact]
    public void baseDamage가_기존_실효값으로_저작돼_있다()
    {
        // 데미지 출처 = ability.baseDamage.
        Assert.Equal(10, AbilityCatalog.Get("basic_swing")!.BaseDamage);
        Assert.Equal(10, AbilityCatalog.Get("heavy_swing")!.BaseDamage);
        Assert.Equal(10, AbilityCatalog.Get("combo_a")!.BaseDamage);
        Assert.Equal(15, AbilityCatalog.Get("combo_b")!.BaseDamage);
        Assert.Equal(25, AbilityCatalog.Get("combo_c")!.BaseDamage);
        Assert.Equal(35, AbilityCatalog.Get("combo_d")!.BaseDamage); // 4단 마무리 타
    }

    [Fact]
    public void onHitEffectIds는_CC_전용이다_데미지_effect_없음()
    {
        // 데미지는 ability.baseDamage 가 소유 → onHit 에는 상태이상만 남는다.
        // 데미지 패킷은 ability_damage 라벨 + 서버 권위 Amount 로 나간다.
        foreach (var ab in AbilityCatalog.All)
            Assert.DoesNotContain(ab.Timeline.OnHitEffectIds, e => e.EndsWith("_dmg"));

        // CC 는 그대로 어빌리티가 소유(예: arachnya = slow_3s).
        Assert.Contains("slow_3s", AbilityCatalog.Get("arachnya_attack")!.Timeline.OnHitEffectIds);
        Assert.Contains("stun_1_5s", AbilityCatalog.Get("gargoyle_attack")!.Timeline.OnHitEffectIds);
    }

    [Fact]
    public void 콤보_타이밍_불변식이_보존된다()
    {
        foreach (var id in new[] { "combo_a", "combo_b", "combo_c", "combo_d" })
        {
            var t = AbilityCatalog.Get(id)!.Timeline;
            Assert.True(t.ComboChainMs > 0, $"{id}: 콤보는 ComboChainMs > 0");
            Assert.True(t.ComboChainMs <= t.ComboWindowMs, $"{id}: ComboChainMs ≤ ComboWindowMs");
        }
    }

    [Fact]
    public void 미등록_어빌리티는_null을_반환한다()
    {
        Assert.Null(AbilityCatalog.Get("does_not_exist"));
        Assert.Null(AbilityCatalog.Get(9999));
    }
}
