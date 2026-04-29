using Game.Main.Character.Input;
using UnityEngine;

namespace Game.Main.Character
{
    public class AttackState : State
    {
        private readonly CharacterMotor _motor;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource _inputSource;
        private readonly CharacterHitEventReceiver _hitEventReceiver;

        public AttackState(
            CharacterMotor motor,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            CharacterHitEventReceiver hitEventReceiver)
        {
            _motor = motor;
            _animations = animations;
            _inputSource = inputSource;
            _hitEventReceiver = hitEventReceiver;
        }

        public override void Enter()
        {
            _motor.Move(Vector3.zero, 0);
            _animations.SetFloat(AnimationFloatType.Speed, 0);
            _hitEventReceiver?.ResetHitTargets();
            _animations.SetTrigger(AnimationTriggerType.Attack);
        }

        public override void Exit()
        {
            return;
        }

        protected override void StateUpdate(float deltaTime)
        {
            return;
        }
    }
}
