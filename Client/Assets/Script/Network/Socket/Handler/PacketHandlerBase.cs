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

        public UniTask HandleAsync(Packet packet)
        {
            return HandleAsync((TPacket)packet);
        }

        public abstract UniTask HandleAsync(TPacket packet);
    }
}
