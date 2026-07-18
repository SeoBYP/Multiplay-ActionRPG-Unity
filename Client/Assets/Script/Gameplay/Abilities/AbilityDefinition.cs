using System.Collections.Generic;
using Game.Gameplay.Character;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Game.Gameplay.Abilities
{
    /// <summary>
    /// 어빌리티(공격·스킬) 1종의 <b>단일 저작 자산</b> — 게임플레이 + 연출(Cue)을 기획자가 여기 하나에서 편집한다.
    /// 플레이어 스킬·몬스터 공격 공용(AC-B). 설계 = ability-so-authoring.md.
    ///
    /// 교리(gas-architecture §2.5): SO 저작 진실원 → `AbilityCatalogExporter` bake → 서버 임베디드 abilities.json.
    ///   · 서버: <b>게임플레이 필드만</b> bake 되어 넘어간다(Cue 는 JSON 에 아예 없음 — "서버는 Cue 를 모른다" 보존).
    ///   · 클라: 이 SO 를 런타임에 직접 조회(`AbilityCatalogProvider`) → 게임플레이 + Cue 둘 다 사용.
    ///
    /// ※ 기존 <see cref="SkillDefinition"/> 의 상위 호환(= 이관 대상). B2 에서 스킬이 옮겨오면 SkillDefinition 은 폐기.
    /// </summary>
    [CreateAssetMenu(fileName = "Ability_", menuName = "Game/Ability Definition", order = 5)]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Tooltip("어빌리티 식별자(서버·클라 공용 키). 예: basic_swing, combo_a, creepy_demon_attack")]
        public string id;

        [Tooltip("패킷 S_AbilityActivated.SkillId(int) 에 실리는 안정 ID. 카탈로그 내 유일. 계약 보존용 — 한번 정하면 바꾸지 않는다.")]
        public int networkId;

        [Header("타임라인 (ms)")]
        public int startupMs = 200;
        public int activeMs = 100;
        public int recoveryMs = 150;
        public int cooldownMs = 400;

        [Tooltip("발동 마나 코스트(서버 검증·차감 / 클라 게이트). 0 = 무료.")]
        public int manaCost = 0;

        [Header("Hitbox (시전자 로컬 기준, 정면 +Z)")]
        public EHitboxShape hitboxShape = EHitboxShape.Box;
        public Vector3 hitboxOffset = new Vector3(0f, 0.5f, 1f);
        [Tooltip("Box=반-크기 / Sphere=X가 반경")]
        public Vector3 hitboxHalfExtents = new Vector3(0.6f, 1f, 0.7f);

        [Header("데미지 (AC-B 안B: 데미지 출처 단일화)")]
        [Tooltip("스탯 스케일 전 base 데미지. 최종 = StatCombatMath.MeleeDamage(baseDamage, 시전자 AttackPower, 대상 Defense).\n" +
                 "플레이어·몬스터 공용 — 데미지는 여기서만 편집한다(effect 의 Health 값이 아니라).")]
        public int baseDamage = 10;

        [Tooltip("이 어빌리티를 쓸 수 있는 사거리(m). 몬스터 AI 가 '지금 쏠 수 있나' 판정에 사용. 플레이어는 hitbox 가 판정하므로 참고값.")]
        public float activationRange = 1.2f;

        [Header("적중 시 부여 효과 — 태그/CC 전용 (GameplayEffectCatalog id)")]
        [Tooltip("AC-B 안B: 데미지는 baseDamage 가 담당한다. 여기엔 상태이상만 넣는다(예: stun_1_5s · slow_3s).")]
        public List<string> onHitEffectIds = new();

        [Header("콤보 타이밍 (서버·클라 공유 — 0 = 콤보 아님)")]
        [Tooltip("이 어빌리티 발동 후 **다음 공격이 나갈 수 있는 최소 시점**(ms) = 애니 체인 지점. 서버가 cadence 를 권위 강제.")]
        public int comboChainMs = 0;

        [Tooltip("이 시간(ms)까지 다음 입력이 없으면 콤보가 끊겨 1단계부터. 불변식: comboChainMs ≤ comboWindowMs.")]
        public int comboWindowMs = 0;

        [Header("Cue (연출) — 클라 전용. bake 되지 않는다")]
        [Tooltip("재생할 애니 트리거의 **의미**(enum). 실제 Animator 파라미터 *이름* 은 프리팹의 CharacterAgentAnimations 가 갖는다 —\n" +
                 "그래야 컨트롤러 파라미터명이 제각각인 몬스터들이 같은 어빌리티를 공유할 수 있다(codemap §2.64).")]
        public AnimationTriggerType cueTrigger = AnimationTriggerType.Attack;

        [Tooltip("ComboStep int 파라미터에 실을 값(콤보 A=0/B=1/C=2). 콤보 미사용이면 0.")]
        public int cueComboStep = 0;

        [Tooltip("타임라인 위의 연출 이벤트(발동 t=0 기준 ms 오프셋에 SFX/VFX 재생). CA-5 타임라인 창에서 편집.\n" +
                 "cueTrigger 는 주 애니(t=0)를, 이 리스트는 그 위에 얹는 소리·이펙트를 담당한다. bake 안 됨(서버 무지).")]
        public List<AbilityCueEvent> cueEvents = new();

        /// <summary>Shared.Gameplay 순수 타입으로 변환(서버와 동일 판정 데이터). 게임플레이만 — Cue 미포함.</summary>
        public SkillTimeline ToTimeline() => new SkillTimeline(
            id, startupMs, activeMs, recoveryMs, cooldownMs,
            new HitboxSpec(
                hitboxShape,
                new NVector3(hitboxOffset.x, hitboxOffset.y, hitboxOffset.z),
                new NVector3(hitboxHalfExtents.x, hitboxHalfExtents.y, hitboxHalfExtents.z)),
            onHitEffectIds, manaCost, comboChainMs, comboWindowMs);
    }
}
