using System;
using Game.Gameplay;

namespace Game.Gameplay.Character
{
    public class GroundedToFallTransition : ITransitionRule
    {
        public StateKind NextState => StateKind.Fall;
        
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