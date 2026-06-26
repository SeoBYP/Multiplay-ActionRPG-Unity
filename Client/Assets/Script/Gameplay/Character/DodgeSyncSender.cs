using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Dungeon 씬 로컬 캐릭터 전용 회피 송신. CharacterSpawner가 Joined 시 동적 AddComponent.
    /// `PlayerCharacterAgent.OnDodgePerformed` → `C_Dodge` 송신 → 서버가 무적 창(DodgeConfig.IframeMs)을
    /// 권위 부여(쿨다운 검증으로 영구 무적 치팅 차단). 대시 이동/위치는 기존 MoveSyncSender(C_Move)가 동기화.
    /// (CombatSyncSender 와 동일 패턴)
    /// </summary>
    public class DodgeSyncSender : MonoBehaviour
    {
        [Inject] private ISocketSession _session;

        private PlayerCharacterAgent _agent;

        private void Awake()
        {
            _agent = GetComponent<PlayerCharacterAgent>();
        }

        private void OnEnable()
        {
            if (_agent != null)
                _agent.OnDodgePerformed += SendDodge;
        }

        private void OnDisable()
        {
            if (_agent != null)
                _agent.OnDodgePerformed -= SendDodge;
        }

        private void SendDodge()
        {
            // 주입 전(AddComponent 직후 OnEnable)·미접속 시 무시.
            if (_session == null || _session.State != SocketSessionState.Joined)
                return;

            _session.SendAsync(new C_Dodge(), destroyCancellationToken).Forget();
        }
    }
}
