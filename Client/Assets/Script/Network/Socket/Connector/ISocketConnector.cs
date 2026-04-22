using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 저수준 TCP 연결과 패킷 송수신 루프를 제공하는 소켓 커넥터 인터페이스.
    /// </summary>
    public interface ISocketConnector : IAsyncDisposable
    {
        bool IsConnected { get; }
        UniTask ConnectAsync(string host, int port, CancellationToken ct);
        UniTask SendAsync(Packet packet, CancellationToken ct);
        UniTask StartReceiveLoopAsync(Func<Packet, UniTask> onPacket, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
