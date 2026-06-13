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
    }

    public enum AnimationFloatType
    {
        None,
        Speed
    }

    public enum AnimationBoolType
    {
        None,
        Grounded
    }

    public class CharacterAgentAnimations : MonoBehaviour
    {
        private Animator m_animator;

        [Header("Animations")] [SerializeField]
        private string m_animationSpeedFloat;

        [SerializeField] private string m_animationGroundedBool;
        [SerializeField] private string m_animationFallTrigger;
        [SerializeField] private string m_animationJumpTrigger;
        [SerializeField] private string m_animationLandTrigger;
        [SerializeField] private string m_animationInteractTrigger;
        [SerializeField] private string m_animationAttackTrigger;
        [SerializeField] private string m_animationDeathTrigger;

        // Mapping enums to animator parameter names
        private Dictionary<AnimationFloatType, string> floatParameters;
        private Dictionary<AnimationBoolType, string> boolParameters;
        private Dictionary<AnimationTriggerType, string> triggerParameters;

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
                // Add more mappings here
            };
            boolParameters = new Dictionary<AnimationBoolType, string>
            {
                { AnimationBoolType.Grounded, m_animationGroundedBool },
                // Add more mappings here
            };
            triggerParameters = new Dictionary<AnimationTriggerType, string>
            {
                { AnimationTriggerType.Jump, m_animationJumpTrigger },
                { AnimationTriggerType.Fall, m_animationFallTrigger },
                { AnimationTriggerType.Land, m_animationLandTrigger },
                { AnimationTriggerType.Interact, m_animationInteractTrigger },
                { AnimationTriggerType.Attack , m_animationAttackTrigger},
                { AnimationTriggerType.Dead , m_animationDeathTrigger}
                // Add more mappings here
            };
        }

        public void SetFloat(AnimationFloatType floatType, float value)
        {
            if(!m_animator) return;
            if (floatParameters.TryGetValue(floatType, out string paramName))
            {
                m_animator.SetFloat(paramName, value);
                return;
            }
        }

        public void SetBool(AnimationBoolType boolType, bool value)
        {
            if(!m_animator) return;
            if (boolParameters.TryGetValue(boolType, out string paramName))
            {
                m_animator.SetBool(paramName, value);
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
