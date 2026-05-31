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
        /// <summary>C_PlayerLeave 패킷을 전송한다. DisconnectAsync 전에 호출해야 한다.</summary>
        UniTask LeaveRoomAsync(CancellationToken ct);
        UniTask SendMoveAsync(C_Move packet, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
