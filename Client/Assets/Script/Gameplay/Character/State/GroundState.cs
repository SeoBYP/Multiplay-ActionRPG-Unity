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
        private readonly AbilitySystemComponent  _abilitySystem;  // null이면 CC(슬로우) 미적용

        private float   _verticalVelocity;
        private float   _animationMovementSpeed;

        // 이동 가감속 램프 — 즉시 최고속 대신 짧은 램프로 수렴해 출발/정지를 부드럽게 한다.
        private float   _currentMoveSpeed;
        private Vector3 _lastMoveInput; // 감속 잔여 이동 방향 (입력이 먼저 끊겨도 관성 유지)

        public GroundState(
            CharacterMotor motor,
            GroundedDetector groundedDetector,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            LocomotionSettings settings,
            AbilitySystemComponent abilitySystem = null)
        {
            _motor           = motor;
            _groundedDetector = groundedDetector;
            _animations      = animations;
            _inputSource     = inputSource;
            _settings        = settings;
            _abilitySystem   = abilitySystem;
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

            // Action 이동잠금(Rooted): 공격/상호작용 발동 중엔 수평 이동을 막는다(중력·회전·락온 facing 은 유지).
            // FSM 전이가 아니라 태그 기반 이동 제약(CA-1). 기존 Slow 태그 게이트와 동일 폴링 패턴.
            if (_abilitySystem != null && _abilitySystem.HasTag(ActionTags.Rooted))
            {
                _currentMoveSpeed = 0f;
                _lastMoveInput = Vector3.zero;
                _motor.Move(new Vector3(0f, _verticalVelocity, 0f), 0f); // 중력만 적용, 수평 0
                _animationMovementSpeed = 0f;
                _animations.SetFloat(AnimationFloatType.Speed, 0f);
                return;
            }

            float targetSpeed = _inputSource.Current.SprintHeld
                ? _settings.SprintSpeed
                : _settings.MoveSpeed;

            // CC: 슬로우 태그가 있으면 이동 속도를 감속(이후 모든 사용처 — Move·애니 — 에 반영).
            if (_abilitySystem != null && _abilitySystem.HasTag(GameplayTags.Slow))
                targetSpeed *= CcConfig.SlowMultiplier;

            // 가감속 램프: 목표 속도로 즉시가 아니라 Acceleration/Deceleration으로 수렴.
            bool hasInput  = _inputSource.Current.Move != Vector2.zero;
            float goal     = hasInput ? targetSpeed : 0f;
            float rate     = goal > _currentMoveSpeed ? _settings.MoveAcceleration : _settings.MoveDeceleration;
            _currentMoveSpeed = Mathf.MoveTowards(_currentMoveSpeed, goal, rate * deltaTime);

            if (hasInput)
                _lastMoveInput = new Vector3(_inputSource.Current.Move.x, 0f, _inputSource.Current.Move.y);

            // 입력이 끊겨도 감속이 끝날 때까지 마지막 방향으로 관성 이동 (미끄럼 없는 정지)
            Vector3 moveInput = hasInput
                ? new Vector3(_inputSource.Current.Move.x, _verticalVelocity, _inputSource.Current.Move.y)
                : new Vector3(_lastMoveInput.x * (_currentMoveSpeed > 0.01f ? 1f : 0f), _verticalVelocity,
                              _lastMoveInput.z * (_currentMoveSpeed > 0.01f ? 1f : 0f));

            _motor.Move(moveInput, _currentMoveSpeed);

            // Animator 파라미터 구동: 이동 속도 → Speed (AnimatorController가 Idle/Walk/Run 블렌드)
            targetSpeed = _inputSource.Current.Move == Vector2.zero ? 0f : targetSpeed;
            _animationMovementSpeed = Mathf.Lerp(
                _animationMovementSpeed,
                targetSpeed,
                deltaTime * _settings.SpeedChangeRate);

            if (_animationMovementSpeed < 0.01f)
                _animationMovementSpeed = 0f;

            _animations.SetFloat(AnimationFloatType.Speed, _animationMovementSpeed);

            DriveStrafeAnimation();
        }

        /// <summary>
        /// 락온(<see cref="CharacterMotor.FacingOverride"/> 설정) 중에는 몸이 타겟을 향한 채 카메라기준 스트레이프로
        /// 이동한다 — 1D Speed(전진 블렌드)로는 옆/뒤 이동이 어색하므로, 이동방향을 facing 프레임으로 분해해
        /// MoveX(좌−/우+)·MoveY(후−/전+)로 2D 방향 블렌드를 구동한다. 비락온이면 Strafe=false(기존 1D).
        /// </summary>
        private void DriveStrafeAnimation()
        {
            bool strafing = _motor.FacingOverride.HasValue;
            _animations.SetBool(AnimationBoolType.Strafe, strafing);
            if (!strafing) return;

            Vector3 fwd = _motor.FacingOverride.Value; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // 크기(0~1) = 현재 이동속도/스프린트속도. 정지=중앙(idle), 최고속=가장자리(run).
            float mag = Mathf.Clamp01(_currentMoveSpeed / Mathf.Max(0.01f, _settings.SprintSpeed));
            Vector3 worldMove = _motor.ResolveWorldMoveDirection(_inputSource.Current.Move);

            _animations.SetFloat(AnimationFloatType.MoveX, Vector3.Dot(worldMove, right) * mag);
            _animations.SetFloat(AnimationFloatType.MoveY, Vector3.Dot(worldMove, fwd) * mag);
        }

        public override void Exit() { }
    }
}
