using System;
using Game.Network.Socket;
using UnityEngine;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// 원격 플레이어 캐릭터 구동기.
    /// ISocketPacketState.OnPlayerMoved 이벤트를 구독해 서버 스냅샷으로 transform을 보간한다.
    /// FSM / Motor / CharacterInputBuffer 없음 — 네트워크 재생 전용.
    /// </summary>
    public class RemoteDriver : MonoBehaviour, IDisposable
    {
        [SerializeField] private float lerpSpeed = 15f;

        public long UserId { get; private set; }

        private Vector3 _targetPos;
        private float   _targetRotY;
        private ISocketPacketState _state;

        public void Initialize(long userId, ISocketPacketState state)
        {
            UserId      = userId;
            _state      = state;
            _targetPos  = transform.position;
            _targetRotY = transform.eulerAngles.y;

            _state.OnPlayerMoved += HandlePlayerMoved;
        }

        private void HandlePlayerMoved(SocketPlayerSnapshot snapshot)
        {
            if (snapshot.UserId != UserId) return;
            _targetPos  = new Vector3(snapshot.PosX, snapshot.PosY, snapshot.PosZ);
            _targetRotY = snapshot.RotY;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * lerpSpeed);

            var euler = transform.eulerAngles;
            euler.y              = Mathf.LerpAngle(euler.y, _targetRotY, Time.deltaTime * lerpSpeed);
            transform.eulerAngles = euler;
        }

        public void Dispose()
        {
            if (_state != null)
                _state.OnPlayerMoved -= HandlePlayerMoved;
        }

        private void OnDestroy() => Dispose();
    }
}
