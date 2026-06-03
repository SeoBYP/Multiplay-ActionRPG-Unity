using System;
using Game.Core;
using Game.Gameplay.Character.Input;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class PlayerCharacterAgent : CharacterAgent
    {
        private InteractionDetector _interactionDetector;

        /// <summary>공격 입력으로 스윙이 발동될 때 발행. 던전 전용 `CombatSyncSender`가 구독해 C_Attack을 송신한다.</summary>
        public event Action OnAttackPerformed;

        protected override void Awake()
        {
            base.Awake();
            _interactionDetector = this.GetAroundComponent<InteractionDetector>();

            // MotionMatchingDriver가 붙어 있으면 MM 연동, 없으면 기존 Animator 방식
            var motionMatching = this.GetAroundComponent<IMotionMatchingDriver>();

            Context = new CharacterStateContext
            {
                Motor                = Motor,
                GroundDetector       = GroundDetector,
                Animations           = AgentAnimations,
                InputSource          = InputSource,
                AbilitySystem        = AbilitySystem,
                LocomotionSettings   = settings,
                MotionMatching       = motionMatching,
            };
        }

        protected override void Update()
        {
            _interactionDetector?.DetectInteractable();
            HandleAttackInput();
            HandleInteractInput();
            base.Update();
        }

        /// <summary>
        /// 공격 = Action 축. 입력→스윙 애니 + OnAttackPerformed 발행.
        /// 적중 판정·데미지는 **서버 권위**(CombatSyncSender가 C_Attack 송신 → 서버 HitboxMath → S_ApplyEffect).
        /// 로컬은 연출만(피격 HitStop은 HitStopController가 HP 감소로 자동 트리거).
        /// </summary>
        private void HandleAttackInput()
        {
            if (InputSource == null || !InputSource.ConsumeAttackPressed())
                return;

            AgentAnimations?.SetTrigger(AnimationTriggerType.Attack);
            OnAttackPerformed?.Invoke();
        }

        /// <summary>
        /// 상호작용 = Action 축. FSM 상태(구 InteractState)가 아니라 입력→탐지된 대상에게 위임.
        /// 대상(문/아이템/NPC)이 IInteractable.Interact(interactor)로 행동을 소유한다(instigator 전달).
        /// ※ 이동 제약/캐스트 타이밍이 필요해지면 GameplayEffect/태그로(전이 아님), 정식화는 후속.
        /// </summary>
        private void HandleInteractInput()
        {
            if (InputSource == null || !InputSource.ConsumeInteractPressed())
                return;

            var target = _interactionDetector != null ? _interactionDetector.CurrentInteractable : null;
            if (target == null)
                return;

            AgentAnimations?.SetTrigger(AnimationTriggerType.Interact);
            target.Interact(gameObject);
        }
    }
}
