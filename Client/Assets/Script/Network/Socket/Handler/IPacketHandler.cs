using System;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 비제네릭 패킷 핸들러 공통 인터페이스.
    /// 디스패처는 이 타입을 통해 모든 핸들러를 동일하게 다룬다.
    /// </summary>
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
