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
        // 패킷 int SkillId → 문자열 스킬 id (0=basic, 1=heavy). 멀티스킬 데이터 주도.
        Assert.Equal("basic_swing", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(0)!.Id);
        Assert.Equal("heavy_swing", global::Server.PacketHandler.Handler.CombatHandler.ResolveSkill(1)!.Id);
    }

    [Fact]
    public void 미등록_스킬은_null을_반환한다()
    {
        Assert.Null(Shared.Infrastructure.Skills.SkillCatalog.Get("does_not_exist"));
    }
}
