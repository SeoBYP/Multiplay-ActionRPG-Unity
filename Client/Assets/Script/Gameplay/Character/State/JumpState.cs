using Game.Gameplay.Character.Input;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class JumpState : State
    {
        private readonly CharacterMotor _motor;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource _inputSource;
        private readonly LocomotionSettings _settings;
        private readonly GasComponent _abilitySystem; // null이면 Rooted 미적용

        private float _verticalVelocity;

        public JumpState(
            CharacterMotor motor,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            LocomotionSettings settings,
            GasComponent abilitySystem = null)
        {
            _motor = motor;
            _animations = animations;
            _inputSource = inputSource;
            _settings = settings;
            _abilitySystem = abilitySystem;
        }

        public override void Enter()
        {
            _verticalVelocity = Mathf.Sqrt(_settings.JumpHeight * -2f * _settings.Gravity);
            _animations.SetTrigger(AnimationTriggerType.Jump);
            _animations.SetBool(AnimationBoolType.Grounded, false);
        }

        protected override void StateUpdate(float deltaTime)
        {
            _verticalVelocity += _settings.Gravity * deltaTime;

            // Action 이동잠금(Rooted): 공중에서도 수평 에어컨트롤을 막는다(중력만 적용). GroundState 와 동일 규약.
            if (_abilitySystem != null && _abilitySystem.HasTag(ActionTags.Rooted))
            {
                _motor.Move(new Vector3(0f, _verticalVelocity, 0f), 0f);
                _animations.SetFloat(AnimationFloatType.Speed, 0f);
                return;
            }

            float targetSpeed = _inputSource.Current.SprintHeld
                ? _settings.SprintSpeed
                : _settings.MoveSpeed;

            targetSpeed = _inputSource.Current.Move == Vector2.zero ? 0f : targetSpeed;

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