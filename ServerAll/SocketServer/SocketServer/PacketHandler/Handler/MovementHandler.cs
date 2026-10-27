using Serilog;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class MovementHandler
{
    [PacketHandler(typeof(C_Move))]
    public static ValueTask HandleMove(Session session, C_Move packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
        {
            return ValueTask.CompletedTask;
        }
        var room = session.Room;

        if (room is null)
        {
            return ValueTask.CompletedTask;
        }
        
        room.Actors.SetPosition(session.UserId, packet.PosX, packet.PosY, packet.PosZ, packet.RotY);
        room.Sessions.Broadcast(BuildBroadcast(session.UserId, packet), session.SessionId);
        
        return ValueTask.CompletedTask;
    }

    // AnimState 는 해석 없이 그대로 중계(연출은 클라 권위).
    internal static S_Move BuildBroadcast(long userId, C_Move packet) => new()
    {
        UserId = userId,
        PosX = packet.PosX,
        PosY = packet.PosY,
        PosZ = packet.PosZ,
        RotY = packet.RotY,
        TimeStamp = packet.TimeStamp,
        AnimState = packet.AnimState,
    };
}