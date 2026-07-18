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
    ///   이동 = 보간된 실제 수평 변위 속도 → Speed(블렌드 임계 0/2/6 은 월드 m/s 라 그대로 대응)
    ///   사망 = OnPlayerDead / 부활 = OnPlayerRevived (CharacterSpawner 의 DownedAllyMarker 와 별개, 연출 전용)
    ///   공격 = OnPlayerAttacked (서버가 S_Attack 브로드캐스트)
    /// Grounded 는 항상 true — 점프/낙하는 동기화되지 않으므로 지상 가정(Jump/Fall 트리거를 쓰지 않는다).
    ///
    /// ⚠ 원격 프리팹에는 WeaponHitbox/Rigidbody 를 붙이지 않는다 — 무기는 <b>메시(연출)뿐</b>.
    ///    원격이 로컬에서 적중 판정을 하면 서버 권위가 깨진다.
    /// </summary>
    public class RemoteDriver : MonoBehaviour, IActorView, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;
        [Tooltip("Speed 파라미터 평활화 계수. 보간 지터가 Idle/Walk/Run 블렌드를 떨게 하는 것을 막는다.")]
        [SerializeField] private float speedSmoothing = 10f;

        public long UserId { get; private set; }

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;

        private CharacterAgentAnimations _animations;
        private AbilityCuePlayer _cuePlayer;
        private float _animSpeed;

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

            _state.OnPlayerMoved    += HandlePlayerMoved;
            _state.OnPlayerDead     += HandlePlayerDead;
            _state.OnPlayerRevived  += HandlePlayerRevived;
            _state.OnPlayerDodged   += HandlePlayerDodged;
            // 공격 연출은 Actor 통합 파이프로 흡수 — S_AbilityActivated → AbilityCueRouter → ActorRegistry → PlayAbilityCue.
            // (CharacterSpawner 가 이 RemoteDriver 를 ActorId(=UserId)로 레지스트리에 등록한다.)

            // 점프/낙하 미동기화 → 지상 가정. Locomotion 이 블렌드에 머물게 한다.
            _animations?.SetBool(AnimationBoolType.Grounded, true);
        }

        private void HandlePlayerMoved(SocketPlayerSnapshot snapshot)
        {
            if (snapshot.UserId != UserId) return;
            _targetPos  = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            _targetRotY = snapshot.RotY;
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
        private void HandlePlayerDodged(long userId)
        {
            if (userId != UserId) return;
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

        /// <summary>보간으로 실제 이동한 수평 거리 → m/s 로 환산해 Speed 에 싣는다(입력이 없으므로 결과에서 역산).</summary>
        private void DriveLocomotionAnimation(Vector3 previous)
        {
            if (_animations == null || Time.deltaTime <= 0f) return;

            var delta = transform.position - previous;
            delta.y = 0f;
            float instant = delta.magnitude / Time.deltaTime;

            _animSpeed = Mathf.Lerp(_animSpeed, instant, Time.deltaTime * speedSmoothing);
            if (_animSpeed < 0.01f) _animSpeed = 0f;

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
