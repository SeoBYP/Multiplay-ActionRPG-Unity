using System;

namespace Game.Gameplay.Character
{
    public class LandToGroundTransition : ITransitionRule
    {
        private float _remainingTime;

        public StateKind NextState => StateKind.Ground;

        public LandToGroundTransition(float duration)
        {
            _remainingTime = duration;
        }

        public bool ShouldTransition(float deltaTime)
        {
            if (_remainingTime <= 0f)
                return true;

            _remainingTime -= deltaTime;
            return false;
        }
    }
}