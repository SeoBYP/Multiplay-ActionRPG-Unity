using System;
using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Dungeon 씬 로컬 캐릭터 전용 네트워크 동기화 컴포넌트.
    /// CharacterSpawner가 Dungeon 진입 시에만 동적으로 AddComponent한다.
    /// transform 변화가 있을 때만 C_Move를 서버로 송신한다.
    /// </summary>
    public class MoveSyncSender : MonoBehaviour
    {
        [Inject] private ISocketSession _session;

        private CharacterAgent _agent;
        private Vector3 _lastPos;
        private float   _lastRotY;
        private byte    _lastAnimState;

        // 로그 스팸 방지 — 1초에 1번만 출력
        private float _logTimer;

        private void Awake() => _agent = GetComponent<CharacterAgent>();

        private void Start()
        {
            _lastPos  = transform.position;
            _lastRotY = transform.eulerAngles.y;
            _lastAnimState = CurrentAnimState();
            Debug.Log("[MoveSyncSender] 초기화 완료 — C_Move 송신 대기 중");
        }

        private void FixedUpdate()
        {
            // Joined 상태가 아니면(끊김/복귀 중) 송신을 건너뛴다 — SendMoveAsync 예외 스팸 방지.
            if (_session == null || _session.State != SocketSessionState.Joined) return;

            var pos  = transform.position;
            var rotY = transform.eulerAngles.y;

            var animState = CurrentAnimState();

            var posDelta = Vector3.Distance(pos, _lastPos);
            var rotDelta = Mathf.Abs(Mathf.DeltaAngle(rotY, _lastRotY));
            // 상태가 바뀌면 제자리여도 보낸다 — 사다리에 붙은 채 멈춰 있어도 원격이 매달린 자세를 잡아야 한다.
            if (posDelta < 0.001f && rotDelta < 0.1f && animState == _lastAnimState) return;

            _lastPos  = pos;
            _lastRotY = rotY;
            _lastAnimState = animState;

            _session.SendMoveAsync(new C_Move
            {
                PosX      = pos.x,
                PosY      = pos.y,
                PosZ      = pos.z,
                RotY      = rotY,
                TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                AnimState = animState,
            }, destroyCancellationToken).Forget();

            // 1초 간격으로만 로그 출력
            _logTimer += Time.fixedDeltaTime;
            if (_logTimer >= 1f)
            {
                _logTimer = 0f;
                Debug.Log($"[MoveSyncSender] C_Move 송신 — pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) rotY={rotY:F1} state={(StateKind)animState}");
            }
        }

        /// <summary>
        /// 현재 로코모션 상태 → 1바이트. FSM 이 없으면(에이전트 미부착) Ground 로 둔다.
        /// 이 값의 의미는 <see cref="StateKind"/> 가 진실원이고 서버는 해석하지 않는다(릴레이만).
        /// </summary>
        private byte CurrentAnimState() => _agent != null ? (byte)_agent.CurrentStateKind : (byte)StateKind.Ground;
    }
}
