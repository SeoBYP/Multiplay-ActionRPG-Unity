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

    /// <summary>
    /// 특정 패킷 타입을 강타입으로 처리하는 제네릭 핸들러 인터페이스.
    /// </summary>
    public interface IPacketHandler<TPacket> : IPacketHandler
        where TPacket : Packet
    {
        UniTask HandleAsync(TPacket packet);
    }
}
