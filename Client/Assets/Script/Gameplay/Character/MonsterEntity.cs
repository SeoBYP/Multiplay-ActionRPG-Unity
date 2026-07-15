using System;
using Game.Network.Socket;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격(서버 권위) 몬스터 구동기. ISocketPacketState 스냅샷으로 위치를 보간하고 애니를 재생한다.
    /// FSM / AI 없음 — RemoteDriver(원격 플레이어)와 동일한 네트워크 재생 전용.
    ///
    /// 애니: 각 _DLNK 몬스터 컨트롤러는 파라미터가 거의 없어(대부분 0개) 공용 파라미터로 구동할 수 없다.
    ///   대신 컨트롤러의 <b>상태 이름</b>(idle/walk/die)을 직접 CrossFade 한다(프리팹에 상태명 직렬화).
    ///   보간 변위 속도 &gt; 임계 → walk, 정지 → idle, OnMonsterDead → die 후 지연 디스폰.
    /// </summary>
    public class MonsterEntity : MonoBehaviour, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;

        [Header("애니(몬스터 컨트롤러의 상태 이름 — 비우면 해당 애니 미재생)")]
        [Tooltip("모델의 Animator. 미할당 시 자식에서 자동 탐색.")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleState = "";
        [SerializeField] private string walkState = "";
        [SerializeField] private string dieState = "";
        [Tooltip("이 속도(m/s) 이상 보간 이동 시 walk, 미만이면 idle.")]
        [SerializeField] private float walkSpeedThreshold = 0.3f;
        [Tooltip("die 애니 재생 후 오브젝트를 파괴하기까지 지연(초).")]
        [SerializeField] private float deathDespawnDelay = 2.0f;
        [SerializeField] private float crossFadeSec = 0.15f;

        public int InstanceId { get; private set; }

        /// <summary>서버 권위 HP/MaxHp. S_MonsterState(→OnMonsterMoved) 로 갱신된다. 체력바가 구독한다.</summary>
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        /// <summary>HP 변경 시 발행(초기 seed 포함). <see cref="MonsterHealthBar"/> 가 구독해 fill 을 갱신.</summary>
        public event Action<MonsterEntity> HpChanged;

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;
        private bool _dead;
        private string _currentState = ""; // 재-CrossFade 방지

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
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

            PlayState(idleState); // 컨트롤러 기본 상태(공격 등)를 idle 로 덮어씀
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

        /// <summary>서버 사망 통지 → die 애니 재생 + 보간 정지 + 지연 디스폰(디스폰은 스포너가 리스트에서 제거).</summary>
        private void HandleDead(int instanceId)
        {
            if (instanceId != InstanceId || _dead) return;
            _dead = true;
            PlayState(dieState);
            // 즉시 파괴하면 die 애니가 안 보인다 → 지연 후 자체 파괴. 스포너는 HandleDead 에서 리스트만 정리.
            Destroy(gameObject, deathDespawnDelay);
        }

        private void Update()
        {
            if (_dead) return;

            var prev = transform.position;
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * lerpSpeed);

            var euler = transform.eulerAngles;
            euler.y               = Mathf.LerpAngle(euler.y, _targetRotY, Time.deltaTime * lerpSpeed);
            transform.eulerAngles = euler;

            // 보간으로 실제 이동한 수평 속도 → walk/idle 전환.
            if (Time.deltaTime > 0f)
            {
                var d = transform.position - prev; d.y = 0f;
                float speed = d.magnitude / Time.deltaTime;
                PlayState(speed >= walkSpeedThreshold ? walkState : idleState);
            }
        }

        /// <summary>상태 이름이 있고 지금 상태와 다르면 CrossFade. 빈 이름/애니터 없음이면 무시(조용히).</summary>
        private void PlayState(string state)
        {
            if (animator == null || string.IsNullOrEmpty(state) || state == _currentState)
                return;
            _currentState = state;
            animator.CrossFadeInFixedTime(state, crossFadeSec);
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
