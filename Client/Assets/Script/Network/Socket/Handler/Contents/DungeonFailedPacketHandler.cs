using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 던전 실패 패킷 처리. 서버가 참가자 전원 다운을 감지해 S_DungeonFailed 를 브로드캐스트하면
    /// 실패 신호(OnDungeonFailed)를 발행한다. Presentation(InGameModel)이 실패 화면→로비 복귀에 사용.
    /// </summary>
    public class DungeonFailedPacketHandler : PacketHandlerBase<S_DungeonFailed>
    {
        private readonly ISocketPacketState _state;

        public DungeonFailedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_DungeonFailed packet)
        {
            Debug.Log($"[DungeonFailedPacketHandler] S_DungeonFailed 수신 — RoomId={packet.RoomId}");
            _state.MarkDungeonFailed();
            return UniTask.CompletedTask;
        }
    }
}
