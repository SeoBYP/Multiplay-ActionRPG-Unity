using Cysharp.Threading.Tasks;
using Game.Network.Socket;
using Game.Network.Socket.Packets;
using Game.System.Auth;
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

        /// <summary>진단 트레이스의 발동자 ActorId 용(플레이어 = +UserId). 전투 로직엔 쓰지 않는다.</summary>
        [Inject] private AuthSession _authSession;

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

        private void SendAttack(int skillId)
        {
            // 주입 전(AddComponent 직후 OnEnable)·미접속 시 무시.
            if (_session == null || _session.State != SocketSessionState.Joined)
                return;

            // 진단(AC-C1b): t_send. 이 시각이 스윙의 기준점이라 **송신 직전**에 찍는다.
            Game.Network.Socket.Diagnostics.CombatTraceRecorder.Shared.RecordAttackSent(
                Game.Network.Socket.Diagnostics.CombatTraceRecorder.NowMs,
                _authSession?.UserId ?? 0, // 플레이어 ActorId = +UserId (ActorIds 규약)
                skillId);

            _session.SendAsync(new C_Attack { SkillId = skillId }, destroyCancellationToken).Forget();
        }
    }
}
