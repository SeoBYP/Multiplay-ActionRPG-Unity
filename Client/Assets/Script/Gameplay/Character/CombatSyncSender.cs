using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Character
{
    /// <summary>
    /// Dungeon 씬 로컬 캐릭터 전용 전투 송신. CharacterSpawner가 Joined 시 동적 AddComponent.
    /// `PlayerCharacterAgent.OnAttackPerformed` → `C_Attack` 송신 → 서버가 권위 적중 판정(HitboxMath).
    /// (적중/데미지는 서버 권위 → S_ApplyEffect. 로컬은 송신만 담당)
    /// </summary>
    public class CombatSyncSender : MonoBehaviour
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
                _agent.OnAttackPerformed += SendAttack;
        }

        private void OnDisable()
        {
            if (_agent != null)
                _agent.OnAttackPerformed -= SendAttack;
        }

        private void SendAttack()
        {
            // 주입 전(AddComponent 직후 OnEnable)·미접속 시 무시.
            if (_session == null || _session.State != SocketSessionState.Joined)
                return;

            _session.SendAsync(new C_Attack { SkillId = 0 }, destroyCancellationToken).Forget();
        }
    }
}
