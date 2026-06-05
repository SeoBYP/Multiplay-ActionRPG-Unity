using Server.Player;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

public static class RoomJoinLeaveHandler
{
    private static S_PlayerJoined ToJoinedPacket(PlayerState state, string mapId) => new()
    {
        Success = true,
        Message = "",
        UserId = state.UserId,
        Nickname = state.Nickname,
        PosX = state.PosX,
        PosY = state.PosY,
        PosZ = state.PosZ,
        RotY = state.RotY,
        MapId = mapId,
        SpawnIndex = state.SpawnIndex
    };

    [PacketHandler(typeof(C_PlayerJoin))]
    public static async ValueTask HandlePlayerJoin(Session session, C_PlayerJoin packet, CancellationToken ct)
    {
        // Redis에서 플레이어 배정 정보 조회
        var key = $"gamesession:player:{packet.UserId}";
        var entries = await session.Redis.HashGetAllAsync(key);
        if (entries.Length == 0)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Player not assigned to any session" }, ct);
            return;
        }

        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
        if (!long.TryParse(dict.GetValueOrDefault("roomId"), out var redisRoomId) || redisRoomId != packet.RoomId)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Room assignment mismatch" }, ct);
            return;
        }

        session.UserId = packet.UserId;
        session.Nickname = dict.GetValueOrDefault("nickname") ?? $"Player_{packet.UserId}";

        var room = session.RoomManager.GetRoom(packet.RoomId);
        if (room is null)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Room not found" }, ct);
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

        var joinedPacket = ToJoinedPacket(playerState, room.MapId);

        // 1) 본인에게 자기 입장 응답(MapId+SpawnIndex 포함 → 클라가 결정론으로 스폰).
        await session.SendPacketAsync(joinedPacket, ct);
        // 2) 기존 입장자들에게 신규 플레이어 통보.
        room.Broadcast(joinedPacket, session.SessionId);

        // 3) 신규 입장자에게 방의 기존 멤버 로스터를 회신.
        //    이게 없으면 늦게 입장한 플레이어가 먼저 들어온 플레이어를 영영 못 본다.
        foreach (var other in room.GetAllPlayerStates())
        {
            if (other.UserId == session.UserId) continue;
            await session.SendPacketAsync(ToJoinedPacket(other, room.MapId), ct);
        }

        // 4) 신규 입장자에게 현재 몬스터 로스터를 회신(서버 권위 스폰 위치/HP).
        //    플레이어 로스터와 같은 이유 — 늦게 입장해도 기존 몬스터를 본다.
        foreach (var monster in room.GetAllMonsters())
        {
            await session.SendPacketAsync(new S_SpawnMonster
            {
                InstanceId = monster.InstanceId,
                MonsterId  = monster.MonsterId,
                PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
                RotY = monster.RotY,
                Hp = monster.Hp, MaxHp = monster.MaxHp,
            }, ct);
        }

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
