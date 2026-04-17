using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
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
                packet.TimeStamp);
            return UniTask.CompletedTask;
        }
    }
}
