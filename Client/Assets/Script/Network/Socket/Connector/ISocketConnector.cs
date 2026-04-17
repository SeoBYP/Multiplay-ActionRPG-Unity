using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public interface ISocketConnector : IAsyncDisposable
    {
        bool IsConnected { get; }
        UniTask ConnectAsync(string host, int port, CancellationToken ct);
        UniTask SendAsync(Packet packet, CancellationToken ct);
        UniTask StartReceiveLoopAsync(Func<Packet, UniTask> onPacket, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }

}