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

        public MovePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_Move packet)
        {
            _state.UpdatePlayerTransform(
                packet.UserId,
                packet.PosX,
                packet.PosY,
                packet.PosZ,
                packet.RotY,
                packet.TimeStamp,
                packet.AnimState); // 연출 상태(점프/낙하/사다리) — 위치로는 복원 불가
            return UniTask.CompletedTask;
        }
    }
}
