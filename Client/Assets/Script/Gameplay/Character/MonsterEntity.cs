using System;
using Game.Network.Socket;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격(서버 권위) 몬스터 구동기. ISocketPacketState.OnMonsterMoved 를 구독해 서버 스냅샷으로 보간한다.
    /// FSM / AI 없음 — RemoteDriver(원격 플레이어)와 동일한 네트워크 재생 전용.
    /// </summary>
    public class MonsterEntity : MonoBehaviour, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;

        public int InstanceId { get; private set; }

        /// <summary>서버 권위 HP/MaxHp. S_MonsterState(→OnMonsterMoved) 로 갱신된다. 체력바가 구독한다.</summary>
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        /// <summary>HP 변경 시 발행(초기 seed 포함). <see cref="MonsterHealthBar"/> 가 구독해 fill 을 갱신.</summary>
        public event Action<MonsterEntity> HpChanged;

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;

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
        }

        private void HandleMoved(SocketMonsterSnapshot snapshot)
        {
            if (snapshot.InstanceId != InstanceId) return;
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

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * lerpSpeed);

            var euler = transform.eulerAngles;
            euler.y               = Mathf.LerpAngle(euler.y, _targetRotY, Time.deltaTime * lerpSpeed);
            transform.eulerAngles = euler;
        }

        public void Dispose()
        {
            if (_state != null)
                _state.OnMonsterMoved -= HandleMoved;
        }

        private void OnDestroy() => Dispose();
    }
}
