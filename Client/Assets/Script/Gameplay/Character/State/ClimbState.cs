using Game.Gameplay.Character.Input;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 사다리 오르내리기(P6) = <b>Locomotion 축의 배타적 이동 모드</b>(CA-1 — Action 이 아니다).
    /// 중력·수평 이동을 끄고 사다리 축(수직)으로만 움직인다.
    ///
    /// 진입: <see cref="GroundToClimbTransition"/>(사다리 상호작용) / 이탈: <see cref="ClimbToGroundTransition"/>(상·하단 도달).
    /// 애니: <c>Climbing</c>(bool)로 상태 전환, <c>ClimbSpeed</c>(-1~1)가 <b>클립 배속</b>이라 음수면 역재생(=내려가기).
    /// 그래서 오르기 클립 하나로 양방향을 표현한다(전용 하강 클립 미사용 — 간결성).
    /// </summary>
    public sealed class ClimbState : State
    {
        private readonly CharacterMotor _motor;
        private readonly CharacterAgentAnimations _animations;
        private readonly ICharacterInputSource _inputSource;
        private readonly ClimbSensor _sensor;
        private readonly LocomotionSettings _settings;

        public ClimbState(
            CharacterMotor motor,
            CharacterAgentAnimations animations,
            ICharacterInputSource inputSource,
            ClimbSensor sensor,
            LocomotionSettings settings)
        {
            _motor = motor;
            _animations = animations;
            _inputSource = inputSource;
            _sensor = sensor;
            _settings = settings;
        }

        public override void Enter()
        {
            // 사다리 정면으로 스냅 — 위치·회전을 한 번에 맞춰 몸이 메시에 박히거나 옆을 보지 않게.
            if (_sensor != null && _sensor.Current != null && _motor != null)
            {
                _sensor.Current.GetAttachPose(_motor.transform.position, out var pos, out var rot);
                _motor.Teleport(pos);
                _motor.transform.rotation = rot;
            }

            _animations?.SetBool(AnimationBoolType.Climbing, true);
            _animations?.SetFloat(AnimationFloatType.Speed, 0f);      // 지상 이동 블렌드 정지
            _animations?.SetFloat(AnimationFloatType.ClimbSpeed, 0f); // 붙은 순간은 정지 포즈
        }

        protected override void StateUpdate(float deltaTime)
        {
            // 전/후 입력만 쓴다(좌우는 무시 — 옆으로 타는 이동은 이번 범위 밖).
            float axis = _inputSource?.Current.Move.y ?? 0f;

            // 점프(Space) = 사다리를 밀어내며 이탈. 전이는 ClimbToFallTransition 이, 밀어내기는 Exit 가 한다.
            if (_inputSource != null && _inputSource.ConsumeJumpPressed())
                _sensor?.RequestJumpOff();

            // 바닥 가까이에서 아래 입력 → 굳이 최하단까지 내려가지 않아도 그냥 내려선다(Idle 복귀).
            if (axis < -0.01f && _sensor?.Current != null && _motor != null &&
                _motor.transform.position.y - _sensor.Current.BottomY <= _settings.ClimbBottomReleaseHeight)
                _sensor.RequestRelease();

            if (Mathf.Abs(axis) > 0.01f)
                _motor?.MoveRaw(Vector3.up * (axis * _settings.ClimbSpeed * deltaTime));

            // 클립 배속 = 입력 축 × (실제 속도 / 클립이 상정한 속도).
            // 실측: Climb_Up 은 1.00m/s 를 상정한다 — 그냥 축(±1)만 넣으면 몸만 빨라져 손발이 발판에서 미끄러진다.
            float speedMul = _settings.ClimbClipSpeed > 0.01f
                ? _settings.ClimbSpeed / _settings.ClimbClipSpeed
                : 1f;
            _animations?.SetFloat(AnimationFloatType.ClimbSpeed, axis * speedMul);
        }

        public override void Exit()
        {
            if (_sensor != null && _motor != null && _sensor.Current != null)
            {
                if (_sensor.JumpOffRequested)
                {
                    // 점프 이탈: 사다리 반대쪽으로 밀어낸 뒤 낙하 상태로 넘긴다(공중 애니는 Fall 이 담당).
                    Vector3 pos = _motor.transform.position;
                    Vector3 away = pos - new Vector3(_sensor.Current.CenterXZ.x, pos.y, _sensor.Current.CenterXZ.z);
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.0001f) away = -_motor.transform.forward;
                    away.Normalize();
                    _motor.Teleport(pos + away * _settings.ClimbJumpOffDistance);
                }
                else if (_sensor.ShouldDetach(_motor.transform.position, out bool atTop) && atTop)
                {
                    // 상단 도달이면 사다리 위로 올라선다(텔레포트) — 그 판정은 센서가 소유.
                    _motor.Teleport(_sensor.Current.GetTopExitPosition(_motor.transform.position));
                }
            }

            _animations?.SetBool(AnimationBoolType.Climbing, false);
            _animations?.SetFloat(AnimationFloatType.ClimbSpeed, 0f);
            _sensor?.Release();
        }
    }
}
