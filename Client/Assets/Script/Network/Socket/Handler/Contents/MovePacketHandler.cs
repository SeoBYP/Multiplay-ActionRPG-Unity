using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 이동 동기화 패킷을 받아 플레이어 위치 스냅샷을 갱신한다.
    /// </summary>
    public class MovePacketHandler : PacketHandlerBase<S_Move>
    {
        private readonly ISocketPacketState _state;

        /// <summary>
        /// 이동 결과를 반영할 상태 저장소를 주입받는다.
        /// </summary>
        public MovePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        /// <summary>
        /// 서버에서 전달된 최신 transform 정보를 저장한다.
        /// </summary>
        public override UniTask HandleAsync(S_Move packet)
        {
            _state.UpdatePlayerTransform(
                packet.UserId,
                packet.PosX,
                packet.PosY,
                packet.PosZ,
                packet.RotY,
                packet.TimeStamp);
            return UniTask.CompletedTask;
        }
    }
}
