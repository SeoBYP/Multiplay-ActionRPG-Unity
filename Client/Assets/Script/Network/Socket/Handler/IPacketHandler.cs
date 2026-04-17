using System;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public interface IPacketHandler
    {
        Type PacketType { get; }
        UniTask HandleAsync(Packet packet);
    }

    public interface IPacketHandler<TPacket> : IPacketHandler
        where TPacket : Packet
    {
        UniTask HandleAsync(TPacket packet);
    }
}