using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class RoomJoinLeaveHandler
{
    [PacketHandler(typeof(C_PlayerJoin))]
    public static async ValueTask HandlePlayerJoin(Session session, C_PlayerJoin packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Authenticate first" }, ct);
            return;
        }

        var room = session.RoomManager.GetRoom(packet.RoomId);
        if (room is null)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Room not found" }, ct);
            return;
        }

        if (!room.IsExpectedPlayer(session.UserId))
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Not assigned to this room" }, ct);
            return;
        }

        if (!session.RoomManager.JoinRoom(session, packet.RoomId))
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Failed to join room" }, ct);
            return;
        }

        var playerState = room.GetPlayerState(session.UserId);
        if (playerState is null)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Player state not initialized" }, ct);
            return;
        }

        var joinedPacket = new S_PlayerJoined
        {
            Success = true,
            Message = "",
            UserId = playerState.UserId,
            Nickname = playerState.Nickname,
            PosX = playerState.PosX,
            PosY = playerState.PosY,
            PosZ = playerState.PosZ,
            RotY = playerState.RotY
        };

        await session.SendPacketAsync(joinedPacket, ct);
        room.Broadcast(joinedPacket, session.SessionId);

        if (room.MemberCount == room.MaxMembers)
        {
            room.Broadcast(new S_GameStatus
            {
                RoomId = room.RoomId,
                GameStatus = EGameStatus.InProgress
            });
        }
    }

    [PacketHandler(typeof(C_PlayerLeave))]
    public static ValueTask HandlePlayerLeave(Session session, C_PlayerLeave packet, CancellationToken ct)
    {
        session.RoomManager.LeaveRoom(session);
        return ValueTask.CompletedTask;
    }
}
