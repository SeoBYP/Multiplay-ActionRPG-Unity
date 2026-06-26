using Game.Gameplay.Character.Input;
using Game.Gameplay;
using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    public class GroundState : State
    {
        private readonly CharacterMotor          _motor;
        private readonly GroundedDetector        _groundedDetector;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource   _inputSource;
        private readonly LocomotionSettings      _settings;
        private readonly IMotionMatchingDriver   _motionMatching; // null이면 기존 Animator 방식
        private readonly AbilitySystemComponent  _abilitySystem;  // null이면 CC(슬로우) 미적용

        private float   _verticalVelocity;
        private float   _animationMovementSpeed;

        public GroundState(
            CharacterMotor motor,
            GroundedDetector groundedDetector,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            LocomotionSettings settings,
            IMotionMatchingDriver motionMatching = null,
            AbilitySystemComponent abilitySystem = null)
        {
            _motor           = motor;
            _groundedDetector = groundedDetector;
            _animations      = animations;
            _inputSource     = inputSource;
            _settings        = settings;
            _motionMatching  = motionMatching;
            _abilitySystem   = abilitySystem;
        }

        public override void Enter()
        {
            _animations.SetBool(AnimationBoolType.Grounded, _groundedDetector.Grounded);
            // MM이 있으면 PlayableGraph 재시작 — AnimatorController에서 MM으로 제어권 이전
            _motionMatching?.Resume();
        }

        protected override void StateUpdate(float deltaTime)
        {
            _verticalVelocity = _groundedDetector.Grounded
                ? 0f
                : _verticalVelocity + _settings.Gravity * deltaTime;

            float targetSpeed = _inputSource.Current.SprintHeld
                ? _settings.SprintSpeed
                : _settings.MoveSpeed;

            // CC: 슬로우 태그가 있으면 이동 속도를 감속(이후 모든 사용처 — Move·MM·애니 — 에 반영).
            if (_abilitySystem != null && _abilitySystem.HasTag(GameplayTags.Slow))
                targetSpeed *= CcConfig.SlowMultiplier;

            _motor.Move(
                new Vector3(_inputSource.Current.Move.x, _verticalVelocity, _inputSource.Current.Move.y),
                targetSpeed);

            if (_motionMatching is { IsActive: true })
            {
                // 입력이 없으면 즉시 zero — CharacterMotor.CurrentVelocity는
                // displacement(방향 × 속도 × deltaTime)이므로 입력 zero 판별에 쓰지 않는다
                Vector3 mmVelocity = _inputSource.Current.Move == Vector2.zero
                    ? Vector3.zero
                    : _motor.DesiredMoveDirection * targetSpeed;
                _motionMatching.SetDesiredVelocity(mmVelocity);
            }
            else
            {
                // MM 없음 또는 비활성: 기존 Animator 파라미터 방식
                targetSpeed = _inputSource.Current.Move == Vector2.zero ? 0f : targetSpeed;
                _animationMovementSpeed = Mathf.Lerp(
                    _animationMovementSpeed,
                    targetSpeed,
                    deltaTime * _settings.SpeedChangeRate);

                if (_animationMovementSpeed < 0.01f)
                    _animationMovementSpeed = 0f;

                _animations.SetFloat(AnimationFloatType.Speed, _animationMovementSpeed);
            }
        }

        public override void Exit()
        {
            // MM이 있으면 PlayableGraph 정지 — AnimatorController로 제어권 반환
            // Jump/Fall/Land/Attack 트리거가 다시 Animator에서 동작한다
            _motionMatching?.Pause();
        }
    }
}
