using System;
using Game.Main.Character.Input;

namespace Game.Main.Character
{
    public class GroundedToJumpTransition : ITransitionRule
    {
        private readonly ICharacterInputSource _inputSource;
        private float _jumpTimeout;

        public Type NextState => typeof(JumpState);

        public GroundedToJumpTransition(ICharacterInputSource inputSource, float jumpTimeout)
        {
            _inputSource = inputSource;
            _jumpTimeout = jumpTimeout;
        }

        public bool ShouldTransition(float deltaTime)
        {
            if (_jumpTimeout > 0f)
            {
                _jumpTimeout -= deltaTime;
                return false;
            }

            return _inputSource.ConsumeJumpPressed();
        }
    }
}