namespace Server.Packet.Handler;

public sealed class RoomHandlers
{
    [PacketHandler("CCreateRoom")]
    public static ValueTask HandleCreateRoom(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        var request = packet.CCreateRoom;
        Console.WriteLine($"[Session {session.SessionId}] C_CreateRoom: max={request.MaxMembers}");
        
        return ValueTask.CompletedTask;
    }

    [PacketHandler("CJoinRoom")]
    public static ValueTask HandleJoinRoom(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        var request = packet.CJoinRoom;
        Console.WriteLine($"[Session {session.SessionId}] C_JoinRoom: room={request.RoomId}");
        return ValueTask.CompletedTask;
    }

    [PacketHandler("CLeaveRoom")]
    public static ValueTask HandleLeaveRoom(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        Console.WriteLine($"[Session {session.SessionId}] C_LeaveRoom");
        return ValueTask.CompletedTask;
    }

    [PacketHandler("CRoomList")]
    public static ValueTask HandleRoomList(Session session, ServerCore.Protocol.Packet packet, CancellationToken ct)
    {
        Console.WriteLine($"[Session {session.SessionId}] C_RoomList");
        return ValueTask.CompletedTask;
    }
}