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
        
        room.UpdatePlayerState(session.UserId, packet.PosX, packet.PosY, packet.PosZ, packet.RotY, packet.TimeStamp);
        room.Broadcast(new S_Move
        {
            UserId = session.UserId,
            PosX = packet.PosX,
            PosY = packet.PosY,
            PosZ = packet.PosZ,
            RotY = packet.RotY,
            TimeStamp = packet.TimeStamp
        }, session.SessionId);
        
        return ValueTask.CompletedTask;
    }
}