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
            // 상태 없음 = 게임 미시작 or 재접속 유예 만료(>ReconnectGraceMs) 후. 재입장 불가.
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Player state not initialized" }, ct);
            return;
        }

        // 입장/재접속 활성화: HasJoined=true(이제부터 몬스터 AI 타깃) + 끊김 유예 보존 상태면 마킹 해제(즉시 복귀).
        room.MarkJoined(session.UserId);

        var joinedPacket = ToJoinedPacket(playerState, room.MapId);

        // 1) 본인에게 자기 입장 응답(MapId+SpawnIndex 포함 → 클라가 결정론으로 스폰).
        await session.SendPacketAsync(joinedPacket, ct);

        // 1b) 본인에게 초기 권위 마나 동기화 — 클라 prefab 기준선(Mana 100)을 서버 권위 MaxMana(레벨테이블,
        //     예: Lv1=50)로 정렬한다. 이후 차감/거부 시점에만 S_PlayerMana 로 정정(리젠은 클라 예측).
        await session.SendPacketAsync(new Shared.Packet.Packets.S_PlayerMana
        {
            UserId = playerState.UserId,
            Mana = playerState.Mana,
            MaxMana = playerState.MaxMana,
        }, ct);
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

        // 5) 신규 입장자에게 현재 바닥 아이템 로스터를 회신(이미 떨어진 드랍을 늦은 입장에도 보이게).
        foreach (var ground in room.GetAllGroundItems())
        {
            await session.SendPacketAsync(new S_SpawnGroundItem
            {
                GroundId = ground.GroundId,
                ItemId = ground.ItemId,
                Qty = ground.Qty,
                PosX = ground.PosX, PosY = ground.PosY, PosZ = ground.PosZ,
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
