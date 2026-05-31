using System;

namespace Game.Gameplay.Character
{
    public class JumpToFallTransition : ITransitionRule
    {
        public StateKind NextState => StateKind.Fall;

        private CharacterMotor m_motor;
        //Delay to let the JumpSate call Move() on the m_mover so that we don't immediately
        //exit from Jump to Fall state
        private float m_checkDelay;

        public JumpToFallTransition(CharacterMotor motor, float checkDelay)
        {
            m_motor = motor;
            m_checkDelay = checkDelay;
        }

        public bool ShouldTransition(float deltaTime)
        {
            if (m_checkDelay <= 0)
                return m_motor.CurrentVelocity.y <= 0;
            m_checkDelay -= deltaTime;
            return false;
        }
    }
}