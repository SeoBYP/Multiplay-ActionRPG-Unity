using System;
using Game.Network.Socket;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격(서버 권위) 몬스터 구동기. ISocketPacketState 스냅샷으로 위치를 보간하고 애니를 재생한다.
    /// FSM / AI 없음 — RemoteDriver(원격 플레이어)와 동일한 네트워크 재생 전용.
    ///
    /// 애니 구동 = <b>Animator 파라미터</b>(RemoteDriver 와 동일 방식, <see cref="CharacterAgentAnimations"/> 경유):
    ///   이동 = 보간된 실제 수평 변위 속도 → Speed(float) → 컨트롤러가 Idle↔Walk 전이
    ///   공격 = 발동 신호(S_AbilityActivated) → Attack(Trigger)
    ///   사망 = OnMonsterDead → Dead(Trigger, 몬스터 컨트롤러의 "Die") 후 지연 디스폰
    /// 파라미터 이름은 프리팹의 CharacterAgentAnimations 에 배선한다(미배선이면 조용히 스킵).
    /// ※ 과거 상태이름 CrossFade 방식은 제거 — 컨트롤러의 Speed 전이와 충돌해 Walk 가 즉시 Idle 로 튕겼다.
    /// </summary>
    public class MonsterEntity : MonoBehaviour, IActorView, IMonsterHealth, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;
        [Tooltip("이 몬스터의 보행 클립이 상정한 이동 속도(m/s). 0 = 미저작(배속 보정 안 함). 제자리 클립이라 자동 계산이 불가해 발 본 후방 이동으로 측정한 값을 저작한다.")]
        [SerializeField] private float walkClipSpeed;

        [Tooltip("Speed 파라미터 평활화 계수. 보간 지터가 Idle/Walk 전이를 떨게 하는 것을 막는다.")]
        [SerializeField] private float speedSmoothing = 10f;
        [Tooltip("die 애니 재생 후 오브젝트를 파괴하기까지 지연(초).")]
        [SerializeField] private float deathDespawnDelay = 2.0f;

        public int InstanceId { get; private set; }

        /// <summary>서버 권위 HP/MaxHp. S_MonsterState(→OnMonsterMoved) 로 갱신된다. 체력바가 구독한다.</summary>
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        /// <summary>HP 변경 시 발행(초기 seed 포함). <see cref="MonsterHealthBar"/> 가 구독해 fill 을 갱신.</summary>
        public event Action<IMonsterHealth> HpChanged;

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;
        private bool _dead;

        private CharacterAgentAnimations _animations;
        private AbilityCuePlayer _cuePlayer;
        private float _animSpeed;

        private void Awake()
        {
            _animations = GetComponent<CharacterAgentAnimations>();
            _cuePlayer = GetComponent<AbilityCuePlayer>();
        }

        public void Initialize(int instanceId, ISocketPacketState state)
        {
            InstanceId  = instanceId;
            _state      = state;
            _targetPos  = transform.position;
            _targetRotY = transform.eulerAngles.y;

            // 스폰 스냅샷(MaxHp 포함)으로 초기 HP seed → 체력바 최초 표시.
            if (_state.TryGetMonster(instanceId, out var snap))
            {
                Hp = snap.Hp;
                MaxHp = snap.MaxHp;
                HpChanged?.Invoke(this);
            }

            _state.OnMonsterMoved += HandleMoved;
            _state.OnMonsterDead  += HandleDead;
        }

        private void HandleMoved(SocketMonsterSnapshot snapshot)
        {
            if (snapshot.InstanceId != InstanceId || _dead) return;
            _targetPos  = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            _targetRotY = snapshot.RotY;

            // S_MonsterState 는 위치+HP 를 함께 실어온다(WithState). HP 가 바뀌면 체력바 갱신 통지.
            if (snapshot.Hp != Hp || snapshot.MaxHp != MaxHp)
            {
                Hp = snapshot.Hp;
                MaxHp = snapshot.MaxHp;
                HpChanged?.Invoke(this);
            }
        }

        /// <summary>서버 사망 통지 → HP 0 확정 + die 애니 트리거 + 보간 정지 + 지연 디스폰(디스폰은 스포너가 리스트에서 제거).</summary>
        private void HandleDead(int instanceId)
        {
            if (instanceId != InstanceId || _dead) return;
            _dead = true;

            // 사망 = HP 0. **서버는 죽는 순간의 S_MonsterState 를 보내지 않는다** — S_MonsterDead 만 온다.
            // 그래서 여기서 0 으로 만들지 않으면 체력바가 **치명타 직전 값에 멈춘 채** die 애니가 재생된다.
            //
            // 왜 서버가 Hp=0 상태를 추가 전송하지 않고 클라가 유도하나:
            //   S_MonsterDead 와 S_MonsterState 는 송신 직렬화가 없어(D1) 순서가 뒤집힐 수 있고,
            //   Dead 가 먼저 도착하면 상태 저장소에서 몬스터가 제거돼 뒤이은 Hp=0 이 **버려진다**(간헐 재발).
            //   "사망 = HP 0" 은 서버가 이미 내린 판정이라 클라가 유도해도 권위를 해치지 않고 순서와 무관하게 항상 맞다.
            if (Hp != 0)
            {
                Hp = 0;
                HpChanged?.Invoke(this);
            }

            _animations?.SetTrigger(AnimationTriggerType.Dead);
            // 즉시 파괴하면 die 애니가 안 보인다 → 지연 후 자체 파괴. 스포너는 HandleDead 에서 리스트만 정리.
            Destroy(gameObject, deathDespawnDelay);
        }

        private void Update()
        {
            if (_dead) return;

            var previous = transform.position;
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * lerpSpeed);

            var euler = transform.eulerAngles;
            euler.y               = Mathf.LerpAngle(euler.y, _targetRotY, Time.deltaTime * lerpSpeed);
            transform.eulerAngles = euler;

            DriveLocomotionAnimation(previous);
        }

        /// <summary>보간으로 실제 이동한 수평 거리 → m/s 로 환산해 Speed 에 싣는다(RemoteDriver 와 동일 — 결과에서 역산).
        /// 컨트롤러가 Speed 임계로 Idle↔Walk 를 전이하므로 여기서 상태를 직접 고르지 않는다.</summary>
        private void DriveLocomotionAnimation(Vector3 previous)
        {
            if (_animations == null || Time.deltaTime <= 0f) return;

            var delta = transform.position - previous;
            delta.y = 0f;
            float instant = delta.magnitude / Time.deltaTime;

            _animSpeed = Mathf.Lerp(_animSpeed, instant, Time.deltaTime * speedSmoothing);
            if (_animSpeed < 0.01f) _animSpeed = 0f;

            _animations.SetFloat(AnimationFloatType.Speed, _animSpeed);
            // 발 슬라이딩 보정 — 클립을 실제 이동 속도에 맞춰 배속(미저작이면 1 = 무보정).
            _animations.SetFloat(AnimationFloatType.MoveSpeedMul,
                LocomotionSpeedMatch.Multiplier(_animSpeed, walkClipSpeed));
        }

        /// <summary>
        /// 발동 연출(IActorView) — 서버 S_AbilityActivated → AbilityCueRouter 가 ActorId 로 이 뷰를 찾고
        /// **어빌리티 카탈로그에서 해석한 Cue** 를 넘겨준다(AC-B B3). 컨트롤러의 트리거 전이가 스윙 재생·복귀를 담당.
        /// ComboStep 파라미터가 없는 몬스터 컨트롤러에선 SetInt 가 조용히 스킵된다(CharacterAgentAnimations 미배선 가드).
        /// </summary>
        public void PlayAbilityCue(AnimationTriggerType trigger, int comboStep)
        {
            if (_dead) return;
            _animations?.SetInt(AnimationIntType.ComboStep, comboStep);
            _animations?.SetTrigger(trigger);
        }

        /// <summary>연출 타임라인(SFX/VFX) 재생 — 라우터가 어빌리티를 넘긴다. 죽었거나 미부착이면 무시(IActorView).</summary>
        public void PlayAbilityCues(Game.Gameplay.Abilities.AbilityDefinition ability)
        {
            if (_dead) return;
            _cuePlayer?.Play(ability);
        }

        public void Dispose()
        {
            if (_state != null)
            {
                _state.OnMonsterMoved -= HandleMoved;
                _state.OnMonsterDead  -= HandleDead;
            }
        }

        private void OnDestroy() => Dispose();
    }
}
