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

            if (Mathf.Abs(axis) > 0.01f)
                _motor?.MoveRaw(Vector3.up * (axis * _settings.ClimbSpeed * deltaTime));

            // 클립 배속 = 입력 축. 정지(0)면 포즈 유지, 음수면 역재생(내려가기).
            _animations?.SetFloat(AnimationFloatType.ClimbSpeed, axis);
        }

        public override void Exit()
        {
            // 상단 도달이면 사다리 위로 올라선다(텔레포트) — 그 판정은 센서가 소유.
            if (_sensor != null && _motor != null &&
                _sensor.ShouldDetach(_motor.transform.position, out bool atTop) && atTop &&
                _sensor.Current != null)
            {
                _motor.Teleport(_sensor.Current.GetTopExitPosition(_motor.transform.position));
            }

            _animations?.SetBool(AnimationBoolType.Climbing, false);
            _animations?.SetFloat(AnimationFloatType.ClimbSpeed, 0f);
            _sensor?.Release();
        }
    }
}
