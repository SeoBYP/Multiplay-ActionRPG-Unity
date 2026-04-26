using Game.Main.Character.Input;
using Script.Main;
using UnityEngine;

namespace Game.Main.Character
{
    public class GroundState : State
    {
        private readonly CharacterMotor _motor;
        private readonly GroundedDetector _groundedDetector;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource _inputSource;
        private readonly LocomotionSettings _settings;

        private float _verticalVelocity;
        private float _animationMovementSpeed;

        public GroundState(
            CharacterMotor motor,
            GroundedDetector groundedDetector,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            LocomotionSettings settings)
        {
            _motor = motor;
            _groundedDetector = groundedDetector;
            _animations = animations;
            _inputSource = inputSource;
            _settings = settings;
        }

        public override void Enter()
        {
            _animations.SetBool(AnimationBoolType.Grounded, _groundedDetector.Grounded);
        }

        protected override void StateUpdate(float deltaTime)
        {
            _verticalVelocity = _groundedDetector.Grounded
                ? 0f
                : _verticalVelocity + _settings.Gravity * deltaTime;

            float targetSpeed = _inputSource.Current.SprintHeld
                ? _settings.SprintSpeed
                : _settings.MoveSpeed;

            _motor.Move(
                new Vector3(_inputSource.Current.Move.x, _verticalVelocity, _inputSource.Current.Move.y),
                targetSpeed);

            targetSpeed = _inputSource.Current.Move == Vector2.zero ? 0f : targetSpeed;
            _animationMovementSpeed = Mathf.Lerp(
                _animationMovementSpeed,
                targetSpeed,
                deltaTime * _settings.SpeedChangeRate);

            if (_animationMovementSpeed < 0.01f)
                _animationMovementSpeed = 0f;

            _animations.SetFloat(AnimationFloatType.Speed, _animationMovementSpeed);
        }

        public override void Exit() { }
    }
}
