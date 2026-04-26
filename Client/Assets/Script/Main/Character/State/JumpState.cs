using Game.Main.Character.Input;
using UnityEngine;

namespace Game.Main.Character
{
    public class JumpState : State
    {
        private readonly CharacterMotor _motor;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource _inputSource;
        private readonly LocomotionSettings _settings;

        private float _verticalVelocity;

        public JumpState(
            CharacterMotor motor,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            LocomotionSettings settings)
        {
            _motor = motor;
            _animations = animations;
            _inputSource = inputSource;
            _settings = settings;
        }

        public override void Enter()
        {
            _verticalVelocity = Mathf.Sqrt(_settings.JumpHeight * -2f * _settings.Gravity);
            _animations.SetTrigger(AnimationTriggerType.Jump);
            _animations.SetBool(AnimationBoolType.Grounded, false);
        }

        protected override void StateUpdate(float deltaTime)
        {
            float targetSpeed = _inputSource.Current.SprintHeld
                ? _settings.SprintSpeed
                : _settings.MoveSpeed;

            targetSpeed = _inputSource.Current.Move == Vector2.zero ? 0f : targetSpeed;

            _verticalVelocity += _settings.Gravity * deltaTime;

            _motor.Move(
                new Vector3(_inputSource.Current.Move.x, _verticalVelocity, _inputSource.Current.Move.y),
                targetSpeed);
        }

        public override void Exit()
        {
            _animations.ResetTrigger(AnimationTriggerType.Jump);
        }
    }
}