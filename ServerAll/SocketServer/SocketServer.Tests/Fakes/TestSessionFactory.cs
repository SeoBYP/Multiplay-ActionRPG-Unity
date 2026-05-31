using System.Net.Sockets;
using Microsoft.Extensions.Logging.Abstractions;
using Server.Room;

namespace Server.Tests.Fakes;

/// <summary>
/// 단위 테스트용 Session 생성 헬퍼.
///
/// Session은 sealed이고 생성자에 실제 Socket을 요구하므로,
/// 연결되지 않은 Socket을 주입한다. LeaveRoom 경로에서는 Socket I/O가
/// 일어나지 않거나(빈 방), 일어나도 SendPacketAsync 내부에서 예외를 삼킨다.
/// PacketDispatcher / IDatabase는 LeaveRoom 경로에서 사용되지 않아 null로 둔다.
/// </summary>
public static class TestSessionFactory
{
    public static Session Create(RoomManager roomManager, ulong sessionId, long userId)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        var session = new Session(
            sessionId,
            socket,
            dispatcher: null!,
            roomManager,
            redis: null!,
            NullLogger<Session>.Instance,
            onDisconnected: null)
        {
            UserId = userId
        };
        return session;
    }
}
