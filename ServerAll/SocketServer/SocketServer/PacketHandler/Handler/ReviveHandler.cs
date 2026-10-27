using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class ReviveHandler
{
    [PacketHandler(typeof(C_Revive))]
    public static ValueTask HandleRevive(Session session, C_Revive packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        var (ok, hp) = room.Progress.TryRevive(session.UserId, packet.TargetUserId);
        if (ok)
            room.Sessions.Broadcast(new S_PlayerRevived { UserId = packet.TargetUserId, Hp = hp });

        return ValueTask.CompletedTask;
    }
}
