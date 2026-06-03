using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 게임 상태 패킷 처리. 전원 입장 시 서버가 InProgress 를 브로드캐스트하면
    /// 던전 준비 완료 신호(OnDungeonReady)를 발행한다. (별도 S_DungeonReady 패킷 없이 S_GameStatus 재사용)
    /// </summary>
    public class GameStatusPacketHandler : PacketHandlerBase<S_GameStatus>
    {
        private readonly ISocketPacketState _state;

        public GameStatusPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_GameStatus packet)
        {
            Debug.Log($"[GameStatusPacketHandler] S_GameStatus 수신 — RoomId={packet.RoomId} Status={packet.GameStatus}");
            if (packet.GameStatus == EGameStatus.InProgress)
                _state.MarkDungeonReady();
            return UniTask.CompletedTask;
        }
    }
}
