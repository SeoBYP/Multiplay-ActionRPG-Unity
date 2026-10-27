using Serilog;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class MovementHandler
{
    private static readonly float MaxSpeed = 8f;
    
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

        if (!float.IsFinite(packet.PosX) || !float.IsFinite(packet.PosY) || !float.IsFinite(packet.PosZ) || !float.IsFinite(packet.RotY))
        {
            return ValueTask.CompletedTask;
        }
        
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (found,cx, cz) = room.Actors.ApplyClientMove(session.UserId, packet.PosX, packet.PosY, packet.PosZ, packet.RotY, nowMs);
        if(found)
        {
            room.Sessions.Broadcast(
                BuildBroadcast(session.UserId, cx, cz, packet), 
                session.SessionId);
        }
        
        return ValueTask.CompletedTask;
    }

    // AnimState 는 해석 없이 그대로 중계(연출은 클라 권위).
    internal static S_Move BuildBroadcast(long userId,float cx, float cz, C_Move packet) => new()
    {
        UserId = userId,
        PosX = cx,
        PosY = packet.PosY,
        PosZ = cz,
        RotY = packet.RotY,
        TimeStamp = packet.TimeStamp,
        AnimState = packet.AnimState,
    };
}