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

            DriveStrafeAnimation(deltaTime);
        }

        /// <summary>
        /// 8방향 이동 애니 구동 — <b>항상</b>(락온 여부 무관). 플레이어의 몸은 <see cref="Rotation.PlayerRotationStrategy"/>
        /// 때문에 늘 <b>카메라 정면</b>을 향하므로(락온이면 타겟), 왼쪽 입력은 실제로 "왼쪽으로 게걸음"이다.
        /// 예전엔 락온일 때만 2D 블렌드를 구동해서 옆/뒤로 가도 전진 클립이 나왔다.
        ///
        /// <b>단위는 m/s</b>(0~1 정규화 아님) — 컨트롤러의 2D 블렌드가 각 클립의 <b>실측 이동 속도</b> 좌표에 놓여 있어
        /// (Walk ≈2.3 · Run ≈3.4 · Sprint 5.335), 같은 단위로 넣어야 블렌드 결과의 발 속도 = 실제 이동 속도가 된다(발 슬라이딩 제거).
        /// </summary>
        private void DriveStrafeAnimation(float deltaTime)
        {
            _animations.SetBool(AnimationBoolType.Strafe, true);

            Vector3 fwd = _motor.FacingOverride ?? _motor.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Vector3 worldMove = _motor.ResolveWorldMoveDirection(_inputSource.Current.Move);
            if (worldMove.sqrMagnitude > 0.0001f) worldMove.Normalize();

            // 실제 이동 속도(m/s)를 facing 프레임으로 분해 → 클립 속도 좌표계와 같은 단위.
            Vector3 velocity = worldMove * _currentMoveSpeed;

            // 감쇠 적용 — 입력 방향은 한 프레임에 꺾이므로(W→A 는 좌표가 3.25 만큼 점프) 그대로 넣으면 클립이 툭 바뀐다.
            // 이동 자체는 즉시 꺾이고 애니만 짧게 따라붙는다(그 사이 발/지면 오차는 감쇠 시간만큼만).
            _animations.SetFloat(AnimationFloatType.MoveX, Vector3.Dot(velocity, right),
                _settings.MoveBlendDamp, deltaTime);
            _animations.SetFloat(AnimationFloatType.MoveY, Vector3.Dot(velocity, fwd),
                _settings.MoveBlendDamp, deltaTime);
        }

        public override void Exit() { }
    }
}
