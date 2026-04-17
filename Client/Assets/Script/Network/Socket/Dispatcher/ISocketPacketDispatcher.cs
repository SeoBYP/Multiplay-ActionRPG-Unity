using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public interface ISocketPacketDispatcher
    {
        UniTask DispatchAsync(Packet packet, CancellationToken ct = default);
    }
}