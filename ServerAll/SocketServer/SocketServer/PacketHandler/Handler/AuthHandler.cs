using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class AuthHandler
{
    [PacketHandler(typeof(C_Auth))]
    public static async ValueTask HandleAuth(Session session, C_Auth packet, CancellationToken ct)
    {
        var room = session.RoomManager.GetAssignedRoom(packet.UserId);
        if (room is null)
        {
            await session.SendPacketAsync(new S_Auth { Success = false, Message = "Not assigned to any room" }, ct);
            return;
        }

        session.UserId = packet.UserId;
        var playerState = room.GetPlayerState(packet.UserId);
        if (playerState is not null)
        {
            session.Nickname = playerState.Nickname;
        }

        await session.SendPacketAsync(new S_Auth { Success = true }, ct);
    }
}
