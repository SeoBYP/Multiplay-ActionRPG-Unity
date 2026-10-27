using System;

namespace Game.Gameplay.Character
{
    public class JumpToFallTransition : ITransitionRule
    {
        public StateKind NextState => StateKind.Fall;

        private CharacterMotor m_motor;
        // JumpState 가 Move() 를 한 번 호출할 시간을 줘 Jump 에서 즉시 Fall 로 빠지지 않게 한다
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