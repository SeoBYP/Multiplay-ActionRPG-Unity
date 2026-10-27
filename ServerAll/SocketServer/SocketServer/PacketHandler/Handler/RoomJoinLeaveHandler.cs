using System.Runtime.CompilerServices;
using Script.System.GamePlayAbilitySystem;
using Server.Room;
using Shared.Packet.Packets;

[assembly: InternalsVisibleTo("SocketServer.Tests")]

namespace Server.PacketHandler.Handler;

public static class RoomJoinLeaveHandler
{
    // internal = 단위 테스트용 시임. 본인응답/타인 브로드캐스트/로스터 3곳이 모두 이 헬퍼를 거친다.
    internal static S_PlayerJoined ToJoinedPacket(RoomMember member, string mapId) => new()
    {
        Success = true,
        Message = "",
        UserId = member.UserId,
        Nickname = member.Nickname,
        PosX = member.Actor.PosX,
        PosY = member.Actor.PosY,
        PosZ = member.Actor.PosZ,
        RotY = member.Actor.RotY,
        MapId = mapId,
        SpawnIndex = member.SpawnIndex,
        Hp = member.Actor.Gas[EGameplayAttribute.Health],
        MaxHp = member.Actor.Gas.Max(EGameplayAttribute.Health)
    };

    [PacketHandler(typeof(C_PlayerJoin))]
    public static async ValueTask HandlePlayerJoin(Session session, C_PlayerJoin packet, CancellationToken ct)
    {
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

        var member = room.Actors.GetMember(session.UserId);
        if (member is null)
        {
            await session.SendPacketAsync(new S_PlayerJoined { Success = false, Message = "Player state not initialized" }, ct);
            return;
        }

        // HasJoined=true(이제부터 AI 타깃) + 유예 보존 상태면 끊김 마킹 해제.
        room.MarkJoined(session.UserId);

        var joinedPacket = ToJoinedPacket(member, room.MapId);

        await session.SendPacketAsync(joinedPacket, ct);

        // 클라 prefab 기준선(Mana 100)을 서버 권위 MaxMana 로 정렬. 이후엔 차감/거부 시에만 정정.
        await session.SendPacketAsync(new Shared.Packet.Packets.S_PlayerMana
        {
            UserId = member.UserId,
            Mana = member.Actor.Gas[EGameplayAttribute.Mana],
            MaxMana = member.Actor.Gas.Max(EGameplayAttribute.Mana),
        }, ct);
        room.Sessions.Broadcast(joinedPacket, session.SessionId);

        // 늦은 입장자가 먼저 있던 플레이어·몬스터·바닥 아이템을 보도록 로스터를 회신한다.
        foreach (var other in room.Actors.Members())
        {
            if (other.UserId == session.UserId) continue;
            await session.SendPacketAsync(ToJoinedPacket(other, room.MapId), ct);
        }

        foreach (var monster in room.Actors.Monsters())
        {
            await session.SendPacketAsync(new S_SpawnMonster
            {
                InstanceId = monster.InstanceId,
                MonsterId  = monster.MonsterId,
                PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
                RotY = monster.RotY,
                Hp = monster.Gas[EGameplayAttribute.Health], MaxHp = monster.Gas.Max(EGameplayAttribute.Health),
            }, ct);
        }

        foreach (var ground in room.Loot.All())
        {
            await session.SendPacketAsync(new S_SpawnGroundItem
            {
                GroundId = ground.GroundId,
                ItemId = ground.ItemId,
                Qty = ground.Qty,
                PosX = ground.PosX, PosY = ground.PosY, PosZ = ground.PosZ,
            }, ct);
        }

        if (room.Sessions.Count == room.MaxMembers)
        {
            room.Sessions.Broadcast(new S_GameStatus
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
