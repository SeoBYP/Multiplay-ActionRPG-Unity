using System;
using Game.Gameplay.Character.Input;

namespace Game.Gameplay.Character
{
    public class GroundToInteractTransition : ITransitionRule
    {
        private readonly ICharacterInputSource _inputSource;
        private readonly InteractionDetector _interactionDetector;

        public StateKind NextState => StateKind.Interact;

        public GroundToInteractTransition(
            ICharacterInputSource inputSource,
            InteractionDetector interactionDetector)
        {
            _inputSource = inputSource;
            _interactionDetector = interactionDetector;
        }

        public bool ShouldTransition(float deltaTime)
        {
            if (!_inputSource.ConsumeInteractPressed())
                return false;

            return _interactionDetector.CurrentInteractable != null;
        }
    }
}
