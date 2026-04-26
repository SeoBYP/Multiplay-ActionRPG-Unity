using System;
using Script.Main;

namespace Game.Main.Character
{
    public class GroundedToFallTransition : ITransitionRule
    {
        public Type NextState => typeof(FallState);
        
        private GroundedDetector m_groundedDetector;

        public GroundedToFallTransition(GroundedDetector groundedDetector)
        {
            m_groundedDetector = groundedDetector;
        }

        public bool ShouldTransition(float deltaTime)
        {
            return m_groundedDetector.Grounded == false && m_groundedDetector.StairsGrounded == false;
        }
    }
}