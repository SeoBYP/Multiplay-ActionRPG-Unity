using System;

namespace Game.Main.Character
{
    public class InteractToGroundTransition : ITransitionRule
    {
        private float _remainingTime;

        public Type NextState => typeof(GroundState);

        public InteractToGroundTransition(float duration)
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
