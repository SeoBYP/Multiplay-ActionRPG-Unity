using System.Collections.Generic;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 스킬 1종의 저작(authoring) 자산. 기획자가 Inspector 에서 스킬당 이 자산 하나를 편집한다.
    /// (DropTable/Monster 와 동일 교리 — SO 저작 진실원 → Export bake → 서버 임베디드 JSON, gas-architecture §2.5.)
    ///
    /// 게임플레이 데이터만(타임라인·hitbox·on-hit). 연출(Cue/VFX/애니)은 포함하지 않는다(클라 전용 별도 트랙).
    /// 런타임: 클라 `SkillCatalogProvider` 가 `ToTimeline()` 로 변환해 LocalCombat 등이 hitbox/쿨다운 조회.
    /// 서버: `SkillCatalogExporter` 가 bake 한 skills.json 을 `Shared.Infrastructure.Skills.SkillCatalog` 가 읽음.
    /// </summary>
    [CreateAssetMenu(fileName = "Skill_", menuName = "Game/Skill Definition", order = 5)]
    public sealed class SkillDefinition : ScriptableObject
    {
        [Tooltip("스킬 식별자(서버·클라 공용 키). 예: basic_swing, heavy_swing")]
        public string id;

        [Header("타임라인 (ms)")]
        public int startupMs = 200;
        public int activeMs = 100;
        public int recoveryMs = 150;
        public int cooldownMs = 400;

        [Tooltip("발동 마나 코스트(서버 검증·차감 / 클라 게이트). 0 = 무료.")]
        public int manaCost = 0;

        [Header("Hitbox (시전자 로컬 기준, 정면 +Z)")]
        public EHitboxShape hitboxShape = EHitboxShape.Box;
        public Vector3 hitboxOffset = new Vector3(0f, 0f, 1f);
        [Tooltip("Box=반-크기 / Sphere=X가 반경")]
        public Vector3 hitboxHalfExtents = new Vector3(0.6f, 0.6f, 0.7f);

        [Header("적중 시 적용 효과 (GameplayEffectCatalog id)")]
        [Tooltip("데미지=basic_attack_dmg, CC=stun_1_5s/slow_3s 등 — 스킬에 효과를 데이터로 부착.")]
        public List<string> onHitEffectIds = new();

        /// <summary>Shared.Gameplay 순수 타입으로 변환(서버와 동일 판정 데이터).</summary>
        public SkillTimeline ToTimeline() => new SkillTimeline(
            id, startupMs, activeMs, recoveryMs, cooldownMs,
            new HitboxSpec(
                hitboxShape,
                new NVector3(hitboxOffset.x, hitboxOffset.y, hitboxOffset.z),
                new NVector3(hitboxHalfExtents.x, hitboxHalfExtents.y, hitboxHalfExtents.z)),
            onHitEffectIds, manaCost);
    }
}
