using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;
using UnityEngine;

namespace Game.Network.Socket
{
    /// <summary>
    /// 원격 공격 연출: 서버가 C_Attack 를 권위 판정한 뒤 방에 S_Attack{AttackerId,SkillId} 를 브로드캐스트하면,
    /// 그 신호(OnPlayerAttacked)를 발행한다. RemoteDriver 가 해당 UserId 의 스윙 애니를 재생한다.
    ///
    /// <b>연출 전용</b> — 적중·데미지는 서버 권위(S_ApplyEffect)로 별도 반영된다. 이 패킷으로 로컬 판정하지 않는다.
    /// </summary>
    public class AttackPacketHandler : PacketHandlerBase<S_Attack>
    {
        private readonly ISocketPacketState _state;

        public AttackPacketHandler(ISocketPacketState state)
        {
            _state = state;
        }

        public override UniTask HandleAsync(S_Attack packet)
        {
            _state.NotifyPlayerAttacked(packet.AttackerId, packet.SkillId);
            return UniTask.CompletedTask;
        }
    }
}
