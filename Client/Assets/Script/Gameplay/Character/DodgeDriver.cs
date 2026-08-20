using Script.System.GamePlayAbilitySystem;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 회피(Dodge) = Action 축 임펄스(FSM 상태 아님 — CA-1). 발동 시 고정 월드 방향으로 대시하고,
    /// 무적 태그(<see cref="GameplayTags.Invulnerable"/>)를 i-frame 동안 세운다.
    ///
    /// 권위:
    ///   - 무적창/쿨다운 수치 = <see cref="DodgeConfig"/>(클라·서버 공유). 던전은 서버가 C_Dodge 로 같은 창을 강제(진실원).
    ///   - 클라 태그는 Main(클라 권위 LocalMonster)에서 실제 피해를 막고, 던전에선 로컬 예측/연출.
    ///
    /// 소유: <see cref="PlayerCharacterAgent"/> 가 인스턴스를 들고 입력 폴링 시 Begin, 활성 동안 Tick 으로 구동하며
    /// 그동안 Locomotion FSM·다른 Action 을 게이트한다(이동 잠금 = 대시가 이동을 전담).
    /// </summary>
    public sealed class DodgeDriver
    {
        private static readonly GameplayTag InvulnerableTag = GameplayTags.Invulnerable;
        private static readonly float IframeSec = DodgeConfig.IframeMs / 1000f;
        private static readonly float CooldownSec = DodgeConfig.CooldownMs / 1000f;

        private readonly CharacterMotor _motor;
        private readonly AbilitySystemComponent _asc;
        private readonly CharacterAgentAnimations _animations;
        private readonly float _dashSpeed;
        private readonly float _dashDuration;

        private bool _active;
        private bool _iframeOn;
        private float _elapsed;
        private Vector3 _dir;
        private float _lastDodgeTime = float.NegativeInfinity;

        public bool IsActive => _active;

        public DodgeDriver(CharacterMotor motor, AbilitySystemComponent asc,
            CharacterAgentAnimations animations, LocomotionSettings settings)
        {
            _motor = motor;
            _asc = asc;
            _animations = animations;
            _dashSpeed = settings?.DodgeSpeed ?? 8f;
            _dashDuration = settings?.DodgeDuration ?? 0.5f;
        }

        /// <summary>쿨다운이 지나 다시 회피할 수 있는가. now = Time.time.</summary>
        public bool CanBegin(float now) => now - _lastDodgeTime >= CooldownSec;

        /// <summary>
        /// 회피 시작 — 방향 락 + 무적 태그 + 애니 트리거. 방향은 호출부가 결정(입력 방향/정면).
        /// 방향은 <b>캐릭터 로컬</b>로 변환해 DodgeX/DodgeY 로 넘긴다 — 컨트롤러의 8방향 Evade 블렌드가 이 값으로 클립을 고른다.
        /// 트리거보다 <b>먼저</b> 세팅해야 전이 시점에 올바른 클립이 선택된다(ComboStep 과 동일 규약).
        /// </summary>
        public void Begin(Vector3 worldDir, float now)
        {
            _active = true;
            _iframeOn = true;
            _elapsed = 0f;
            _dir = worldDir;
            _lastDodgeTime = now;

            _asc?.AddTag(InvulnerableTag);

            // 월드 방향 → 캐릭터 로컬(전/후/좌/우). 정규화해 블렌드 가장자리(각 방향 클립)에 정확히 걸리게 한다.
            if (_motor != null)
            {
                Vector3 local = _motor.transform.InverseTransformDirection(worldDir);
                local.y = 0f;
                if (local.sqrMagnitude > 0.0001f) local.Normalize();
                else local = Vector3.forward; // 방향 소실 시 정면 구르기
                _animations?.SetFloat(AnimationFloatType.DodgeX, local.x);
                _animations?.SetFloat(AnimationFloatType.DodgeY, local.z);
            }

            _animations?.SetTrigger(AnimationTriggerType.Dodge);
        }

        /// <summary>활성 동안 매 프레임 구동. 대시 이동 + i-frame 만료 시 태그 해제 + 종료 판정.</summary>
        public void Tick(float dt)
        {
            if (!_active) return;
            _elapsed += dt;

            // i-frame 만료 → 무적 태그 해제(대시 길이와 독립 — 설정이 어긋나도 태그가 남지 않게).
            if (_iframeOn && _elapsed >= IframeSec)
            {
                _iframeOn = false;
                _asc?.RemoveTag(InvulnerableTag);
            }

            // 대시 이동은 대시 지속 동안만(그 뒤 무적이 남아 있으면 제자리 유지).
            if (_elapsed < _dashDuration)
                _motor?.Dash(_dir, _dashSpeed);

            // 대시·무적 모두 끝나면 제어 반환(FSM 재개).
            if (_elapsed >= _dashDuration && !_iframeOn)
                _active = false;
        }

        /// <summary>강제 종료(부활/씬 전환 등). 무적 태그가 남아있으면 해제.</summary>
        public void Cancel()
        {
            _active = false;
            if (_iframeOn)
            {
                _iframeOn = false;
                _asc?.RemoveTag(InvulnerableTag);
            }
        }
    }
}
