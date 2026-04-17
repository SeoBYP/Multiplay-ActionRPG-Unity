using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public class PlayerJoinedPacketHandler : PacketHandlerBase<S_PlayerJoined>
    {
        private readonly ISocketPacketState _state;

        public PlayerJoinedPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_PlayerJoined packet)
        {
            if (!packet.Success)
            {
                return UniTask.CompletedTask;
            }

            _state.UpsertPlayer(
                packet.UserId,
                packet.Nickname,
                packet.PosX,
                packet.PosY,
                packet.PosZ,
                packet.RotY);
            return UniTask.CompletedTask;
        }
    }
}
