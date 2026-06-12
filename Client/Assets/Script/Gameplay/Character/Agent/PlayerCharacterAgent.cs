using System;
using Game.Core;
using Game.Gameplay.Character.Input;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class PlayerCharacterAgent : CharacterAgent
    {
        // 사망 상태 태그(GAS). HP 0 시 세우고, 입력/이동 게이트가 폴링한다. 던전=다운-잠금(씬 복귀 전까지 유지).
        private static readonly GameplayTag DeadTag = GameplayTags.Dead;

        private InteractionDetector _interactionDetector;

        /// <summary>공격 입력으로 스윙이 발동될 때 발행. 던전 전용 `CombatSyncSender`가 구독해 C_Attack을 송신한다.</summary>
        public event Action OnAttackPerformed;

        /// <summary>로컬 플레이어가 사망(State.Dead)했는지. 입력·이동 게이트의 단일 판정.</summary>
        private bool IsDead => AbilitySystem != null && AbilitySystem.HasTag(DeadTag);

        protected override void Awake()
        {
            base.Awake();
            _interactionDetector = this.GetAroundComponent<InteractionDetector>();

            // 자기 HP 를 관찰해 0 이하가 되면 State.Dead 를 세운다(클라 결정론 HP 기준).
            if (AbilitySystem != null)
                AbilitySystem.OnAttributeChanged += OnAttributeChanged;

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
            // 사망(다운) 시 두 축 모두 게이트: Action(공격/상호작용) 무시 + base.Update() 미호출로
            // Locomotion(이동) 정지. 던전 내 부활(2.5.2) 또는 씬 복귀 전까지 다운-잠금 유지.
            if (IsDead)
                return;

            _interactionDetector?.DetectInteractable();
            HandleAttackInput();
            HandleInteractInput();
            base.Update();
        }

        /// <summary>HP(클라 결정론)가 0 이하가 되면 State.Dead 를 1회 세운다. 이후 게이트가 작동한다.</summary>
        private void OnAttributeChanged(EGameplayAttribute type, int current, int max)
        {
            if (type == EGameplayAttribute.Health && current <= 0 && AbilitySystem != null && !IsDead)
                AbilitySystem.AddTag(DeadTag);
        }

        private void OnDestroy()
        {
            if (AbilitySystem != null)
                AbilitySystem.OnAttributeChanged -= OnAttributeChanged;
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
