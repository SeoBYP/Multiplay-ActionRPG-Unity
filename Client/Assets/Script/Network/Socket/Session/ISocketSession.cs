using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public interface ISocketSession
    {
        SocketSessionState State { get; }
        UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct);
        UniTask JoinRoomAsync(CancellationToken ct);
        UniTask SendMoveAsync(C_Move packet, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
