using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class AuthHandler
{
    [PacketHandler(typeof(C_Auth))]
    public static async ValueTask HandleAuth(Session session, C_Auth packet, CancellationToken ct)
    {
        
        // GameStart MQ로 생성된 Room에서 UserId 검증
        var room = session.RoomManager.GetRoom(packet.RoomId);
        if (room is null)
        {
            await session.SendPacketAsync(new S_Auth { Success = false, Message = "Room not found" }, ct);
            return;
        }

        if (!room.IsExpectedPlayer(packet.UserId))
        {
            await session.SendPacketAsync(new S_Auth { Success = false, Message = "Not authorized" }, ct);
            return;
        }
        
        // Room에 Join
        session.UserId = packet.UserId;
        room.Join(session);

        await session.SendPacketAsync(new S_Auth { Success = true }, ct);
    }
}
