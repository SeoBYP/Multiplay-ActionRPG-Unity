using Script.System.GamePlayAbilitySystem;

namespace Server.Tests.Combat;

/// <summary>
/// 2.2 스킬 데이터 자산화 — 임베디드 skills.json(클라 SkillDefinition SO 저작→bake) 로드 검증.
/// 서버 CombatHandler 가 이 데이터로 발동 게이트·hitbox·on-hit 를 권위 판정한다.
/// </summary>
public class SkillCatalogTests
{
    [Fact]
    public void 임베디드_basic_swing_정의가_로드된다()
    {
        var skill = Shared.Infrastructure.Skills.SkillCatalog.Get("basic_swing");

        Assert.NotNull(skill);
        Assert.Equal(200, skill!.StartupMs);
        Assert.Equal(100, skill.ActiveMs);
        Assert.Equal(150, skill.RecoveryMs);
        Assert.Equal(400, skill.CooldownMs);
        Assert.Equal(0, skill.ManaCost); // 기본 공격은 무료
        Assert.Equal(EHitboxShape.Box, skill.Hitbox.Shape);
        Assert.Contains("basic_attack_dmg", skill.OnHitEffectIds);
    }

    [Fact]
    public void 임베디드_heavy_swing_정의가_로드된다()
    {
        var skill = Shared.Infrastructure.Skills.SkillCatalog.Get("heavy_swing");

        Assert.NotNull(skill);
        Assert.Equal(400, skill!.StartupMs);   // 느린 강공격
        Assert.Equal(1200, skill.CooldownMs);
        Assert.Equal(20, skill.ManaCost);      // 강공격 마나 코스트
        Assert.Contains("basic_attack_dmg", skill.OnHitEffectIds);
    }

    [Fact]
    public void ResolveSkill_은_skillId를_스킬로_매핑한다()
    {
        // 패킷 int SkillId → 문자열 스킬 id (0=basic, 1=heavy, 2/3/4=combo). 멀티스킬 데이터 주도.
        Assert.Equal("basic_swing", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(0)!.Id);
        Assert.Equal("heavy_swing", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(1)!.Id);
        Assert.Equal("combo_a", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(2)!.Id);
        Assert.Equal("combo_b", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(3)!.Id);
        Assert.Equal("combo_c", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(4)!.Id);
    }

    [Fact]
    public void 임베디드_콤보_스킬_3종이_로드되고_리치가_단계별_상승한다()
    {
        // #7 콤보 A→B→C — bake 된 skills.json 에서 로드. 단계별 hitbox 리치·데미지 이펙트가 상승.
        var a = Shared.Infrastructure.Skills.SkillCatalog.Get("combo_a");
        var b = Shared.Infrastructure.Skills.SkillCatalog.Get("combo_b");
        var c = Shared.Infrastructure.Skills.SkillCatalog.Get("combo_c");

        Assert.NotNull(a); Assert.NotNull(b); Assert.NotNull(c);
        Assert.Contains("combo_a_dmg", a!.OnHitEffectIds);
        Assert.Contains("combo_b_dmg", b!.OnHitEffectIds);
        Assert.Contains("combo_c_dmg", c!.OnHitEffectIds);

        // 정면 리치(half-Z) 단계별 상승 A<B<C (마무리 C 가 가장 넓다).
        Assert.True(a.Hitbox.HalfExtents.Z < b.Hitbox.HalfExtents.Z, "combo_a 리치 < combo_b");
        Assert.True(b.Hitbox.HalfExtents.Z < c.Hitbox.HalfExtents.Z, "combo_b 리치 < combo_c");
    }

    [Fact]
    public void 미등록_스킬은_null을_반환한다()
    {
        Assert.Null(Shared.Infrastructure.Skills.SkillCatalog.Get("does_not_exist"));
    }
}
