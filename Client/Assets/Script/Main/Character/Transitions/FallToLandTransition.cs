using System;
using Script.Main;

namespace Game.Main.Character
{
    public class FallToLandTransition : ITransitionRule
    {
        public Type NextState => typeof(LandState);
        
        private GroundedDetector m_groundedDetector;

        public FallToLandTransition(GroundedDetector mGroundedDetector)
        {
            m_groundedDetector = mGroundedDetector;
        }

        public bool ShouldTransition(float deltaTime)
        {
            return m_groundedDetector.Grounded;
        }


    }
}