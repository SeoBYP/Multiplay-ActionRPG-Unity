using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public enum AnimationTriggerType
    {
        None,
        Jump,
        Fall,
        Land,
        Interact,
        Attack,
        Dead, // 사망(다운) 포즈. HP≤0 시 PlayerCharacterAgent 가 트리거. 리스폰 시 ResetTrigger.
        Dodge, // 회피/구르기. 입력 시 DodgeDriver 가 트리거(클립 미배선이면 조용히 스킵).
        Revive, // 부활 — Dead 포즈에서 로코모션으로 복귀. 부활 시 트리거(Dead 는 나가는 전이가 없어 양성 신호 필요).
        AttackSpecial, // AC-D1: 강스킬 전용 공격(보스 슬램 등) — 평타 Attack 과 구분되는 별도 클립. 미배선 몬스터/캐릭터는 조용히 스킵(파라미터명 빈 값).

        // ⚠️ 이 enum 은 AbilityDefinition.cueTrigger 로 **SO 에 정수로 직렬화**된다 → 중간 삽입 금지(기존 저작값이 밀린다).
        //    실제로 P5 에서 Hit 을 중간에 넣었다가 보스 슬램의 AttackSpecial(9) 이 Hit 으로 읽혀 테스트가 잡았다. 새 값은 항상 끝에 추가한다.
        Hit, // 피격 리액션(P5) — HP 가 줄었을 때 1회. 사망(HP≤0)에는 쓰지 않는다(Dead 가 담당).
    }

    public enum AnimationFloatType
    {
        None,
        Speed,
        MoveX, // 락온 strafe — facing 기준 좌(-)/우(+) 이동 성분
        MoveY, // 락온 strafe — facing 기준 후(-)/전(+) 이동 성분
        DodgeX, // 회피 방향(P5) — 캐릭터 로컬 좌(-)/우(+). 8방향 Evade 블렌드 선택용
        DodgeY, // 회피 방향(P5) — 캐릭터 로컬 후(-)/전(+)
        ClimbSpeed, // 사다리(P6) — -1~1. 컨트롤러가 이 값을 **클립 배속**으로 쓴다(음수=역재생=내려가기)
        MoveSpeedMul // 보행 클립 배속 — 실제 이동 속도 / 클립이 상정한 속도. 발 슬라이딩 보정(몬스터)
    }

    public enum AnimationIntType
    {
        None,
        ComboStep // 근접 콤보 단계(0=A/1=B/2=C) — 컨트롤러가 이 값으로 콤보 상태 선택
    }

    public enum AnimationBoolType
    {
        None,
        Grounded,
        Strafe, // 락온 중 = true → 2D 방향 블렌드로 전환
        Climbing // 사다리(P6) 부착 중 = true → Climb 상태로 전환
    }

    public class CharacterAgentAnimations : MonoBehaviour
    {
        private Animator m_animator;

        [Header("Animations")] [SerializeField]
        private string m_animationSpeedFloat;

        [SerializeField] private string m_animationMoveXFloat;
        [SerializeField] private string m_animationMoveYFloat;
        [SerializeField] private string m_animationDodgeXFloat;
        [SerializeField] private string m_animationDodgeYFloat;
        [SerializeField] private string m_animationClimbSpeedFloat;
        [SerializeField] private string m_animationMoveSpeedMulFloat;

        [SerializeField] private string m_animationComboStepInt;

        [SerializeField] private string m_animationGroundedBool;
        [SerializeField] private string m_animationStrafeBool;
        [SerializeField] private string m_animationClimbingBool;
        [SerializeField] private string m_animationFallTrigger;
        [SerializeField] private string m_animationJumpTrigger;
        [SerializeField] private string m_animationLandTrigger;
        [SerializeField] private string m_animationInteractTrigger;
        [SerializeField] private string m_animationAttackTrigger;
        [SerializeField] private string m_animationDeathTrigger;
        [SerializeField] private string m_animationDodgeTrigger;
        [SerializeField] private string m_animationHitTrigger;
        [SerializeField] private string m_animationReviveTrigger;
        [SerializeField] private string m_animationAttackSpecialTrigger; // AC-D1: 강스킬 전용 공격(보스 슬램 등). 컨트롤러마다 파라미터명 다를 수 있어 여기서 흡수.

        // Mapping enums to animator parameter names
        private Dictionary<AnimationFloatType, string> floatParameters;
        private Dictionary<AnimationBoolType, string> boolParameters;
        private Dictionary<AnimationTriggerType, string> triggerParameters;
        private Dictionary<AnimationIntType, string> intParameters;

        private void Awake()
        {
            m_animator = GetComponentInChildren<Animator>();
            InitializeParameterMappings();
        }

        private void InitializeParameterMappings()
        {
            floatParameters = new Dictionary<AnimationFloatType, string>
            {
                { AnimationFloatType.Speed, m_animationSpeedFloat },
                { AnimationFloatType.MoveX, m_animationMoveXFloat },
                { AnimationFloatType.MoveY, m_animationMoveYFloat },
                { AnimationFloatType.DodgeX, m_animationDodgeXFloat },
                { AnimationFloatType.DodgeY, m_animationDodgeYFloat },
                { AnimationFloatType.ClimbSpeed, m_animationClimbSpeedFloat },
                { AnimationFloatType.MoveSpeedMul, m_animationMoveSpeedMulFloat },
                // Add more mappings here
            };
            boolParameters = new Dictionary<AnimationBoolType, string>
            {
                { AnimationBoolType.Grounded, m_animationGroundedBool },
                { AnimationBoolType.Strafe, m_animationStrafeBool },
                { AnimationBoolType.Climbing, m_animationClimbingBool },
                // Add more mappings here
            };
            intParameters = new Dictionary<AnimationIntType, string>
            {
                { AnimationIntType.ComboStep, m_animationComboStepInt },
            };
            triggerParameters = new Dictionary<AnimationTriggerType, string>
            {
                { AnimationTriggerType.Jump, m_animationJumpTrigger },
                { AnimationTriggerType.Fall, m_animationFallTrigger },
                { AnimationTriggerType.Land, m_animationLandTrigger },
                { AnimationTriggerType.Interact, m_animationInteractTrigger },
                { AnimationTriggerType.Attack , m_animationAttackTrigger},
                { AnimationTriggerType.Dead , m_animationDeathTrigger},
                { AnimationTriggerType.Dodge , m_animationDodgeTrigger},
                { AnimationTriggerType.Hit , m_animationHitTrigger},
                { AnimationTriggerType.Revive , m_animationReviveTrigger},
                { AnimationTriggerType.AttackSpecial, m_animationAttackSpecialTrigger } // AC-D1
                // Add more mappings here
            };
        }

        /// <summary>
        /// 감쇠(damping) 버전 — 값을 즉시 꽂지 않고 <paramref name="dampTime"/> 초에 걸쳐 수렴시킨다.
        /// 방향 전환처럼 <b>파라미터가 한 프레임에 튀는</b> 경우, 블렌드 트리는 그 값을 그대로 따라가 클립이 툭 바뀐다.
        /// Unity 내장 감쇠를 쓰는 이유: 프레임률에 무관하고 Animator 평가 시점과 어긋나지 않는다.
        /// </summary>
        public void SetFloat(AnimationFloatType floatType, float value, float dampTime, float deltaTime)
        {
            if (!m_animator) return;
            if (dampTime <= 0f) { SetFloat(floatType, value); return; }
            if (floatParameters.TryGetValue(floatType, out string paramName) && !string.IsNullOrEmpty(paramName))
                m_animator.SetFloat(paramName, value, dampTime, deltaTime);
        }

        public void SetFloat(AnimationFloatType floatType, float value)
        {
            if(!m_animator) return;
            // 파라미터명이 비어있으면(미배선 — 예: 원격/NPC 의 MoveX) 조용히 스킵. Animator 경고 방지.
            if (floatParameters.TryGetValue(floatType, out string paramName) && !string.IsNullOrEmpty(paramName))
            {
                m_animator.SetFloat(paramName, value);
                return;
            }
        }

        public void SetBool(AnimationBoolType boolType, bool value)
        {
            if(!m_animator) return;
            if (boolParameters.TryGetValue(boolType, out string paramName) && !string.IsNullOrEmpty(paramName))
            {
                m_animator.SetBool(paramName, value);
                return;
            }
        }

        public void SetInt(AnimationIntType intType, int value)
        {
            if(!m_animator) return;
            // 파라미터명이 비어있으면(미배선 — 예: NPC 콤보 미사용) 조용히 스킵.
            if (intParameters.TryGetValue(intType, out string paramName) && !string.IsNullOrEmpty(paramName))
            {
                m_animator.SetInteger(paramName, value);
                return;
            }
        }

        public void SetTrigger(AnimationTriggerType triggerType)
        {
            if(!m_animator) return;
            // 파라미터명이 비어있으면(미배선 — 예: Dead 클립 아직 안 만듦) 조용히 스킵. Animator 경고 방지.
            if (triggerParameters.TryGetValue(triggerType, out string paramName) && !string.IsNullOrEmpty(paramName))
            {
                m_animator.SetTrigger(paramName);
                return;
            }
        }

        public void ResetTrigger(AnimationTriggerType triggerType)
        {
            if(!m_animator) return;
            if (triggerParameters.TryGetValue(triggerType, out string paramName) && !string.IsNullOrEmpty(paramName))
            {
                m_animator.ResetTrigger(paramName);
                return;
            }
        }
    }
}
