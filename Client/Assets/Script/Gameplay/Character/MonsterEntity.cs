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

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;

        public void Initialize(int instanceId, ISocketPacketState state)
        {
            InstanceId  = instanceId;
            _state      = state;
            _targetPos  = transform.position;
            _targetRotY = transform.eulerAngles.y;

            _state.OnMonsterMoved += HandleMoved;
        }

        private void HandleMoved(SocketMonsterSnapshot snapshot)
        {
            if (snapshot.InstanceId != InstanceId) return;
            _targetPos  = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            _targetRotY = snapshot.RotY;
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
