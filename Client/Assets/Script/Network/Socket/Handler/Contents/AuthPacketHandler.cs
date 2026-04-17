using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public class AuthPacketHandler : PacketHandlerBase<S_Auth>
    {
        private readonly ISocketPacketState _state;

        public AuthPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_Auth packet)
        {
            _state.SetAuthResult(packet.Success, packet.Message);
            return UniTask.CompletedTask;
        }
    }
}
