using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Network.Socket.Packets;

namespace Game.Network.Socket
{
    public interface ISocketSession
    {
        SocketSessionState State { get; }

        /// <summary>의도치 않은 연결 끊김(서버 다운/네트워크 절단 등) 발생 시 1회 발화. 정상 DisconnectAsync(퇴장)에서는 발화하지 않는다. 메인 스레드에서 호출됨.</summary>
        event Action OnDisconnected;
        UniTask ConnectAsync(SocketConnectionInfo connectionInfo, CancellationToken ct);
        UniTask JoinRoomAsync(CancellationToken ct);
        /// <summary>C_PlayerLeave 패킷을 전송한다. DisconnectAsync 전에 호출해야 한다.</summary>
        UniTask LeaveRoomAsync(CancellationToken ct);
        UniTask SendMoveAsync(C_Move packet, CancellationToken ct);
        /// <summary>임의 패킷 송신(Joined 상태에서만). C_Attack 등 게임플레이 패킷용.</summary>
        UniTask SendAsync(Packet packet, CancellationToken ct);
        UniTask DisconnectAsync(CancellationToken ct);
    }
}
