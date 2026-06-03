using Game.Core;
using Game.Gameplay.Character.Input;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class PlayerCharacterAgent : CharacterAgent
    {
        private InteractionDetector _interactionDetector;
        private CharacterHitEventReceiver _hitEventReceiver;

        protected override void Awake()
        {
            base.Awake();
            _interactionDetector = this.GetAroundComponent<InteractionDetector>();
            _hitEventReceiver    = this.GetAroundComponent<CharacterHitEventReceiver>();

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
        /// 공격 = Action 축. FSM 상태(구 AttackState)가 아니라 입력→스윙으로 처리한다.
        /// 데미지는 Animation Event → CharacterHitEventReceiver → BasicAttackAbility(GAS) 체인.
        /// (쿨다운·active window·정식 GAS 어빌리티화는 CA-3에서.)
        /// </summary>
        private void HandleAttackInput()
        {
            if (InputSource == null || !InputSource.ConsumeAttackPressed())
                return;

            _hitEventReceiver?.ResetHitTargets();
            AgentAnimations?.SetTrigger(AnimationTriggerType.Attack);
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
