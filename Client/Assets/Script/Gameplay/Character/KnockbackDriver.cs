using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 넉백(밀려남) = 외부에서 부여하는 강제 변위 임펄스. 회피(DodgeDriver)와 형제 — 고정 월드 방향으로
    /// 일정 거리를 일정 시간에 걸쳐 Motor 로 밀어낸다(회전 없음). 무적/쿨다운 없음(순수 변위).
    ///
    /// 소유: <see cref="PlayerCharacterAgent"/> 가 <c>ApplyKnockback</c> 에서 Begin, 활성 동안 Tick 으로 구동하며
    /// 그동안 입력/Locomotion 을 게이트한다(스턴보다 우선 — 맞아 밀려나는 중엔 임펄스가 이동을 전담).
    ///
    /// 지금은 테스트/몬스터 배선용. 추후 GameplayEffect/Ability 가 ApplyKnockback 으로 융합한다(방향·세기 데이터화).
    /// </summary>
    public sealed class KnockbackDriver
    {
        private readonly CharacterMotor _motor;

        private bool _active;
        private float _elapsed;
        private float _duration;
        private float _speed;
        private Vector3 _dir;

        public bool IsActive => _active;

        public KnockbackDriver(CharacterMotor motor)
        {
            _motor = motor;
        }

        /// <summary>넉백 시작 — 월드 방향으로 distance 만큼 duration 초에 걸쳐 민다. 무효 입력(0 방향/시간)은 무시.</summary>
        public void Begin(Vector3 worldDir, float distance, float duration)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f || duration <= 0f)
                return;

            _dir = worldDir.normalized;
            _duration = duration;
            _speed = distance / duration;
            _elapsed = 0f;
            _active = true;
        }

        /// <summary>활성 동안 매 프레임 구동. 일정 속도로 밀어내고 duration 경과 시 종료.</summary>
        public void Tick(float dt)
        {
            if (!_active) return;
            _elapsed += dt;

            if (_elapsed >= _duration)
            {
                _active = false;
                return;
            }

            _motor?.Dash(_dir, _speed, faceDirection: false); // 회전 없이 밀려남
        }

        /// <summary>강제 종료(부활/씬 전환 등).</summary>
        public void Cancel() => _active = false;
    }
}
