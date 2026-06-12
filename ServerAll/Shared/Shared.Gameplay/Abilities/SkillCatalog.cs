using System.Collections.Generic;
using System.Numerics;

namespace Script.System.GamePlayAbilitySystem
{
    /// <summary>
    /// SkillId → SkillTimeline 조회. 1단계 코드 시드(SO/JSON 진실원 아님 — plan 준수).
    /// 추후 공유 JSON 로더가 같은 Register API로 채운다(저작 툴 = CA-5).
    /// </summary>
    public sealed class SkillCatalog
    {
        private readonly Dictionary<string, SkillTimeline> _skills = new();

        public SkillCatalog()
        {
            SeedDefaults();
        }

        public void Register(SkillTimeline skill)
        {
            if (skill != null)
                _skills[skill.Id] = skill;
        }

        public SkillTimeline? Get(string id)
            => id != null && _skills.TryGetValue(id, out var s) ? s : null;

        public bool TryGet(string id, out SkillTimeline? skill)
        {
            skill = null;
            return id != null && _skills.TryGetValue(id, out skill);
        }

        // 1단계 시드 — 기본 근접 공격. on-hit = GameplayEffectCatalog 키(데미지/디버프).
        private void SeedDefaults()
        {
            Register(new SkillTimeline(
                id: "basic_swing",
                startupMs: 200,
                activeMs: 100,
                recoveryMs: 150,
                cooldownMs: 400,
                hitbox: new HitboxSpec(
                    EHitboxShape.Box,
                    offset: new Vector3(0f, 0f, 1f),
                    halfExtents: new Vector3(0.6f, 0.6f, 0.7f)),
                onHitEffectIds: new[] { "basic_attack_dmg" }));
        }
    }
}
