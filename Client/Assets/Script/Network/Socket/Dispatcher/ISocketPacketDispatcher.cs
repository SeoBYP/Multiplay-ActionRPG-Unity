using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 역직렬화된 패킷을 적절한 핸들러에 위임하는 디스패처 인터페이스.
    /// </summary>
    public interface ISocketPacketDispatcher
    {
        UniTask DispatchAsync(Packet packet, CancellationToken ct = default);
    }
}
