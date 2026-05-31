using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 다른 플레이어가 방을 나갔을 때 로컬 상태에서 해당 플레이어를 제거한다.
    /// </summary>
    public class PlayerLeftPacketHandler : PacketHandlerBase<S_PlayerLeft>
    {
        private readonly ISocketPacketState _state;

        public PlayerLeftPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_PlayerLeft packet)
        {
            Debug.Log($"[PlayerLeftPacketHandler] 플레이어 퇴장 — UserId={packet.UserId}");
            _state.RemovePlayer(packet.UserId);
            return UniTask.CompletedTask;
        }
    }
}
