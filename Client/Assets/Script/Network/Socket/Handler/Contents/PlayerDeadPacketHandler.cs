using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 2.5.1 ⓔ-2: 개별 다운(원격 가시성). 서버가 C_PlayerDead 수신 시 S_PlayerDead{UserId} 를 방에
    /// 브로드캐스트하면, 그 신호(OnPlayerDead)를 발행한다. CharacterSpawner 가 해당 캐릭터를
    /// 다운 처리한다(현재 로그+Destroy, 다운 포즈는 후속).
    /// </summary>
    public class PlayerDeadPacketHandler : PacketHandlerBase<S_PlayerDead>
    {
        private readonly ISocketPacketState _state;

        public PlayerDeadPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_PlayerDead packet)
        {
            Debug.Log($"[PlayerDeadPacketHandler] S_PlayerDead 수신 — UserId={packet.UserId}");
            _state.NotifyPlayerDead(packet.UserId);
            return UniTask.CompletedTask;
        }
    }
}
