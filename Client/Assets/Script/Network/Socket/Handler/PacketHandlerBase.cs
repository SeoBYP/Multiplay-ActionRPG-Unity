using System;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 패킷 다운캐스팅 공통 로직을 제공하는 핸들러 베이스 클래스.
    /// </summary>
    public abstract class PacketHandlerBase<TPacket> : IPacketHandler<TPacket>
        where TPacket : Packet
    {
        public Type PacketType => typeof(TPacket);

        /// <summary>
        /// 비제네릭 인터페이스 호출을 강타입 핸들러로 연결한다.
        /// </summary>
        public UniTask HandleAsync(Packet packet)
        {
            return HandleAsync((TPacket)packet);
        }

        /// <summary>
        /// 실제 패킷 처리 로직은 파생 클래스에서 구현한다.
        /// </summary>
        public abstract UniTask HandleAsync(TPacket packet);
    }
}
