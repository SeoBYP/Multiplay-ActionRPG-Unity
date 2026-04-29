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
            _interactionDetector = GetComponent<InteractionDetector>();
            _hitEventReceiver = GetComponent<CharacterHitEventReceiver>();
            
            Context = new CharacterStateContext
            {
                Motor = Motor,
                GroundDetector = GroundDetector,
                Animations = AgentAnimations,
                InputSource = InputSource,
                AbilitySystem = AbilitySystem,
                InteractionDetector = _interactionDetector,
                HitEventReceiver = _hitEventReceiver,
                LocomotionSettings = settings
            };
        }
        
        protected override void Update()
        {
            _interactionDetector?.DetectInteractable();
            base.Update();
        }
    }
}
