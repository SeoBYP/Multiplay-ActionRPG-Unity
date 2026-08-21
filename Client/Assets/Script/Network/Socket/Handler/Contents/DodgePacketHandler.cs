using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 원격 회피 연출: 서버가 C_Dodge 를 쿨다운·마나 검증한 뒤 방에 S_Dodge{UserId} 를 브로드캐스트하면,
    /// 그 신호(OnPlayerDodged)를 발행한다. RemoteDriver 가 해당 UserId 의 회피(구르기) 애니를 재생한다.
    ///
    /// <b>연출 전용</b> — 무적 창/피해 무시는 서버 권위. 이 패킷으로 로컬 판정하지 않는다(S_Attack 과 동일).
    /// </summary>
    public class DodgePacketHandler : PacketHandlerBase<S_Dodge>
    {
        private readonly ISocketPacketState _state;

        public DodgePacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_Dodge packet)
        {
            _state.NotifyPlayerDodged(packet.UserId, packet.DirX, packet.DirY);
            return UniTask.CompletedTask;
        }
    }
}
