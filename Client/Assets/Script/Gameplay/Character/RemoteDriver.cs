using System;
using Game.Network.Socket;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격 플레이어 캐릭터 구동기.
    /// ISocketPacketState 이벤트를 구독해 서버 스냅샷으로 transform을 보간하고, 애니메이터를 구동한다.
    /// FSM / Motor / CharacterInputBuffer 없음 — 네트워크 재생 전용.
    ///
    /// 애니 구동(로컬과 달리 입력이 아니라 <b>수신 스냅샷</b>이 소스):
    ///   이동 = 보간 속도를 facing 프레임으로 분해 → <b>MoveX/MoveY(8방향, m/s)</b> — 로컬과 같은 공식·같은 단위.
    ///          방향은 위치·회전에서 역산되므로 패킷에 싣지 않는다(<see cref="RemoteLocomotion"/>).
    ///   모드 = S_Move.AnimState(Ground/Jump/Fall/Land/Climb) — 이건 역산이 불가능해 1바이트로 받는다.
    ///          점프·낙하·사다리는 전부 "y 가 변한다"로 같아 위치만으로는 구분할 수 없다.
    ///   사망 = OnPlayerDead / 부활 = OnPlayerRevived (CharacterSpawner 의 DownedAllyMarker 와 별개, 연출 전용)
    ///   공격 = S_AbilityActivated → AbilityCueRouter (아래 Initialize 참조)
    ///
    /// ⚠ 원격 프리팹에는 WeaponHitbox/Rigidbody 를 붙이지 않는다 — 무기는 <b>메시(연출)뿐</b>.
    ///    원격이 로컬에서 적중 판정을 하면 서버 권위가 깨진다.
    /// </summary>
    public class RemoteDriver : MonoBehaviour, IActorView, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;
        [Tooltip("이동 블렌드 평활화 계수. 보간 지터가 Idle/Walk/Run 블렌드를 떨게 하는 것을 막는다.")]
        [SerializeField] private float speedSmoothing = 10f;

        public long UserId { get; private set; }

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;

        private CharacterAgentAnimations _animations;
        private AbilityCuePlayer _cuePlayer;
        private float _animSpeed;
        private Vector2 _animMove;          // 평활화된 MoveX/MoveY(m/s)
        private StateKind _animState = StateKind.Ground;
        private float _lastTargetY;         // 사다리 배속 산출용 — 목표 y 로 재야 보간 지연이 안 섞인다
        private float _climbSpeed;

        private void Awake()
        {
            _animations = GetComponent<CharacterAgentAnimations>();
            _cuePlayer = GetComponent<AbilityCuePlayer>();
        }

        public void Initialize(long userId, ISocketPacketState state)
        {
            UserId      = userId;
            _state      = state;
            _targetPos  = transform.position;
            _targetRotY = transform.eulerAngles.y;
            _lastTargetY = _targetPos.y;

            _state.OnPlayerMoved    += HandlePlayerMoved;
            _state.OnPlayerDead     += HandlePlayerDead;
            _state.OnPlayerRevived  += HandlePlayerRevived;
            _state.OnPlayerDodged   += HandlePlayerDodged;
            // 공격 연출은 Actor 통합 파이프로 흡수 — S_AbilityActivated → AbilityCueRouter → ActorRegistry → PlayAbilityCue.
            // (CharacterSpawner 가 이 RemoteDriver 를 ActorId(=UserId)로 레지스트리에 등록한다.)

            _animations?.SetBool(AnimationBoolType.Grounded, true);
            // 로컬과 동일하게 8방향 블렌드를 쓴다. 끄면 1D 트리라 옆걸음·뒷걸음이 전부 전진 클립으로 보인다.
            _animations?.SetBool(AnimationBoolType.Strafe, true);
        }

        private void HandlePlayerMoved(SocketPlayerSnapshot snapshot)
        {
            if (snapshot.UserId != UserId) return;

            // 사다리 배속은 <b>목표 y</b> 의 변화로 잰다 — 보간된 실제 y 로 재면 lerp 지연만큼 늘 작게 나온다.
            float dy = snapshot.PosY - _lastTargetY;
            _lastTargetY = snapshot.PosY;

            _targetPos  = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            _targetRotY = snapshot.RotY;

            ApplyAnimState((StateKind)snapshot.AnimState, dy);
        }

        /// <summary>
        /// 로코모션 <b>모드</b> 반영. 트리거는 상태가 바뀌는 순간에만 쏜다 —
        /// 이동 패킷마다 쏘면 Jump/Land 애니가 매 프레임 리셋돼 제자리에서 떤다.
        /// </summary>
        private void ApplyAnimState(StateKind next, float dy)
        {
            if (next == StateKind.Climb)
            {
                // 오르내림 배속: 위로 갈수록 +, 아래로 갈수록 −(클립 역재생 = 내려가기). 로컬 ClimbState 와 같은 규약.
                _climbSpeed = Mathf.Clamp(dy * 10f, -1.5f, 1.5f);
                _animations?.SetFloat(AnimationFloatType.ClimbSpeed, _climbSpeed);
            }

            if (next == _animState) return;
            _animState = next;

            _animations?.SetBool(AnimationBoolType.Climbing, next == StateKind.Climb);
            _animations?.SetBool(AnimationBoolType.Grounded, next == StateKind.Ground || next == StateKind.Land);

            switch (next)
            {
                case StateKind.Jump: _animations?.SetTrigger(AnimationTriggerType.Jump); break;
                case StateKind.Fall: _animations?.SetTrigger(AnimationTriggerType.Fall); break;
                case StateKind.Land: _animations?.SetTrigger(AnimationTriggerType.Land); break;
                case StateKind.Climb: break; // bool 기반 전이 — 트리거 없음
                default:
                    _animations?.SetFloat(AnimationFloatType.ClimbSpeed, 0f);
                    break;
            }
        }

        /// <summary>다운 포즈(연출). 캐릭터는 남는다 — 부활 대상이라 CharacterSpawner 가 DownedAllyMarker 를 붙인다.</summary>
        private void HandlePlayerDead(long userId)
        {
            if (userId != UserId) return;
            _animations?.ResetTrigger(AnimationTriggerType.Revive); // 이전 부활 트리거 잔재 제거
            _animations?.SetTrigger(AnimationTriggerType.Dead);
        }

        /// <summary>부활 — Dead 포즈에서 로코모션 복귀(로컬 ReviveInPlace 와 동일 규약).</summary>
        private void HandlePlayerRevived(long userId, int hp)
        {
            if (userId != UserId) return;
            _animations?.ResetTrigger(AnimationTriggerType.Dead);
            _animations?.SetTrigger(AnimationTriggerType.Revive);
        }

        /// <summary>원격 공격 스윙(연출 전용, IActorView). 적중·데미지는 서버 권위 — 여기선 애니만 재생한다.
        /// AbilityCueRouter 가 ActorId 로 이 뷰를 찾고 **어빌리티 카탈로그에서 Cue 를 해석해** 넘긴다 —
        /// 과거의 하드코딩 콤보 switch(3→B/4→C)는 제거됐다(AC-B B3: 연출은 Ability SO 저작).</summary>
        public void PlayAbilityCue(AnimationTriggerType trigger, int comboStep)
        {
            _animations?.SetInt(AnimationIntType.ComboStep, comboStep);
            _animations?.SetTrigger(trigger);
        }

        /// <summary>연출 타임라인(SFX/VFX) 재생 — 라우터가 어빌리티를 넘긴다. AbilityCuePlayer 미부착이면 무시(IActorView).</summary>
        public void PlayAbilityCues(Game.Gameplay.Abilities.AbilityDefinition ability) => _cuePlayer?.Play(ability);

        /// <summary>원격 회피 구르기(연출 전용). 무적 창/피해 무시는 서버 권위 — 여기선 애니만 재생한다.</summary>
        private void HandlePlayerDodged(long userId, float dirX, float dirY)
        {
            if (userId != UserId) return;
            // 방향은 S_Dodge 가 실어 온다(캐릭터 기준 우+/전+). 트리거보다 <b>먼저</b> 세팅해야
            // 전이 시점에 8방향 Evade 블렌드가 올바른 클립을 고른다(로컬 DodgeDriver 와 같은 규약).
            _animations?.SetFloat(AnimationFloatType.DodgeX, dirX);
            _animations?.SetFloat(AnimationFloatType.DodgeY, dirY);
            _animations?.SetTrigger(AnimationTriggerType.Dodge);
        }

        private void Update()
        {
            var previous = transform.position;

            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * lerpSpeed);

            var euler = transform.eulerAngles;
            euler.y              = Mathf.LerpAngle(euler.y, _targetRotY, Time.deltaTime * lerpSpeed);
            transform.eulerAngles = euler;

            DriveLocomotionAnimation(previous);
        }

        /// <summary>
        /// 보간으로 <b>실제 이동한</b> 변위 → m/s 속도 → facing 프레임 분해(MoveX/MoveY).
        /// 입력이 없으니 결과에서 역산한다. 단위를 m/s 로 유지해야 블렌드 결과의 발 속도 = 이동 속도가 된다.
        /// Speed(1D) 도 함께 채운다 — 사망/피격 등 1D 로 떨어지는 경로의 하위호환.
        /// </summary>
        private void DriveLocomotionAnimation(Vector3 previous)
        {
            if (_animations == null || Time.deltaTime <= 0f) return;

            Vector3 velocity = (transform.position - previous) / Time.deltaTime;
            velocity.y = 0f;

            Vector2 target = RemoteLocomotion.ToFacingFrame(velocity, transform.eulerAngles.y);

            float t = Time.deltaTime * speedSmoothing;
            _animMove = Vector2.Lerp(_animMove, target, t);
            if (_animMove.sqrMagnitude < 0.0001f) _animMove = Vector2.zero;

            _animSpeed = Mathf.Lerp(_animSpeed, velocity.magnitude, t);
            if (_animSpeed < 0.01f) _animSpeed = 0f;

            _animations.SetFloat(AnimationFloatType.MoveX, _animMove.x);
            _animations.SetFloat(AnimationFloatType.MoveY, _animMove.y);
            _animations.SetFloat(AnimationFloatType.Speed, _animSpeed);
        }

        public void Dispose()
        {
            if (_state == null) return;
            _state.OnPlayerMoved    -= HandlePlayerMoved;
            _state.OnPlayerDead     -= HandlePlayerDead;
            _state.OnPlayerRevived  -= HandlePlayerRevived;
            _state.OnPlayerDodged   -= HandlePlayerDodged;
        }

        private void OnDestroy() => Dispose();
    }
}
