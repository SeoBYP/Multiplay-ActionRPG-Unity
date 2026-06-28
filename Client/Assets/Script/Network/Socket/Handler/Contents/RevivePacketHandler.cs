using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// Co-op 부활(2.5.2). 서버가 C_Revive 를 검증(권위)하고 S_PlayerRevived{UserId, Hp} 를 방에
    /// 브로드캐스트하면 그 신호(OnPlayerRevived)를 발행한다. CharacterSpawner 가 로컬=제자리 부활 /
    /// 원격=다운 보존 해제로 처리한다.
    /// </summary>
    public class RevivePacketHandler : PacketHandlerBase<S_PlayerRevived>
    {
        private readonly ISocketPacketState _state;

        public RevivePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_PlayerRevived packet)
        {
            Debug.Log($"[RevivePacketHandler] S_PlayerRevived 수신 — UserId={packet.UserId} Hp={packet.Hp}");
            _state.NotifyPlayerRevived(packet.UserId, packet.Hp);
            return UniTask.CompletedTask;
        }
    }
}
