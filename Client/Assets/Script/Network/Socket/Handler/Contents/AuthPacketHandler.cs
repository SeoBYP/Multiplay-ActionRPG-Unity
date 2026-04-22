using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 인증 응답 패킷을 받아 소켓 인증 상태 저장소를 갱신한다.
    /// </summary>
    public class AuthPacketHandler : PacketHandlerBase<S_Auth>
    {
        private readonly ISocketPacketState _state;

        /// <summary>
        /// 인증 결과를 반영할 상태 저장소를 주입받는다.
        /// </summary>
        public AuthPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        /// <summary>
        /// 서버 인증 성공/실패 결과를 로컬 상태에 반영한다.
        /// </summary>
        public override UniTask HandleAsync(S_Auth packet)
        {
            _state.SetAuthResult(packet.Success, packet.Message);
            return UniTask.CompletedTask;
        }
    }
}
