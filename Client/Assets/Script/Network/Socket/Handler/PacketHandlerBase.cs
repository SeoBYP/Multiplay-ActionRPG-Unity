using System;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
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