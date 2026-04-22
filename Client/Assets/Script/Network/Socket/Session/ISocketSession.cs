using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    /// <summary>
    /// 소켓 연결부터 인증, 방 참가, 이동 전송까지 상위 흐름을 캡슐화한 세션 인터페이스.
    /// </summary>
    public interface ISocketSession
    {
        SocketSessionState State { get; }
        UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct);
        UniTask AuthenticateAsync(CancellationToken ct);
        UniTask JoinRoomAsync(CancellationToken ct);
        UniTask SendMoveAsync(C_Move packet, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
