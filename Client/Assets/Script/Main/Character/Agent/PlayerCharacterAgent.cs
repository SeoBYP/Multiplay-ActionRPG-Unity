using Game.Core;
using Game.Main.Character.Input;
using UnityEngine;

namespace Game.Main.Character
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
                InteractionDetector  = _interactionDetector,
                HitEventReceiver     = _hitEventReceiver,
                LocomotionSettings   = settings,
                MotionMatching       = motionMatching,
            };
        }
        
        protected override void Update()
        {
            _interactionDetector?.DetectInteractable();
            base.Update();
        }
    }
}
