using System.Globalization;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class DungeonRoomRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<DungeonRoomRepository> logger) : IDungeonRoomRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4,
        string mapId = "", CancellationToken ct = default)
    {
        try
        {
            // mapId 정규화·검증은 Application 책임 — 받은 값을 그대로 보관
            var room = Domain.Entities.DungeonRoom.Create(roomName, hostId, maxPlayers, mapId);

            var entry = await context.DungeonRooms.AddAsync(room, ct);
            await context.SaveChangesAsync(ct);

            var createdRoom = entry.Entity;

            await SetDungeonRoomCacheAsync(createdRoom);

            return createdRoom;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room for host {HostUserId}", hostId);
            throw;
        }
    }

    public async Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.DungeonRoom(roomId));
            if (entries.Length > 0)
                return ParseDungeonRoomFromRedis(roomId, entries);

            // AsNoTracking 필수: 스트리밍 RPC 가 DbContext 를 오래 유지해, 추적 쿼리는 stale 엔티티를 돌려주고
            // 다른 스코프가 쓴 변경을 못 읽는다.
            var room = await context.DungeonRooms.AsNoTracking().SingleOrDefaultAsync(r => r.RoomId == roomId, ct);
            if (room is null)
                return null;

            await SetDungeonRoomCacheAsync(room);
            return room;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var player = await context.DungeonRoomPlayers
                .AsNoTracking()
                .SingleOrDefaultAsync(drp => drp.UserId == userId, ct);

            if (player is null)
                return null;

            return await GetByIdAsync(player.RoomId, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room by user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default)
    {
        try
        {
            // 목록의 진실은 DB 다. `room:active` 는 집합 전체에 TTL 이 걸려 멤버십이 흔들리므로,
            // 믿으면 살아 있는 방이 사라지거나 페이징에서 같은 방이 두 페이지에 나온다.
            return await context.DungeonRooms
                .AsNoTracking()
                .Where(r => r.Status != RoomStatus.Closed)
                .ToListAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get all active dungeon rooms");
            throw;
        }
    }

    public async Task<ActiveRoomsPage> GetActiveRoomsPageAsync(int offset, int limit, CancellationToken ct = default)
    {
        try
        {
            // 전량을 읽어 메모리에서 자르지 않는다 — 정렬·건너뛰기·자르기는 DB 가 한다.
            var query = context.DungeonRooms.AsNoTracking().Where(r => r.Status != RoomStatus.Closed);

            var total = await query.LongCountAsync(ct);
            var rooms = await query
                .OrderByDescending(r => r.RoomId)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(ct);

            return new ActiveRoomsPage(rooms, total);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get active dungeon room page (offset {Offset}, limit {Limit})", offset, limit);
            throw;
        }
    }

    public async Task<long> GetActiveRoomCountAsync(CancellationToken ct = default)
    {
        // 목록과 같은 이유로 `room:active` 크기를 세지 않는다 (집합 전체 TTL 로 실제보다 작게 나옴).
        return await context.DungeonRooms.CountAsync(r => r.Status != RoomStatus.Closed, ct);
    }

    public async Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room, CancellationToken ct = default)
    {
        try
        {
            if (room.RoomId <= 0)
                throw new InvalidOperationException("UpdateAsync는 기존 방만 업데이트 가능");

            var existingRoom = await context.DungeonRooms
                .AsNoTracking()
                .SingleOrDefaultAsync(r => r.RoomId == room.RoomId, ct);

            if (existingRoom == null)
                return false;

            context.DungeonRooms.Update(room);
            await context.SaveChangesAsync(ct);

            await DeleteDungeonRoomCacheAsync(room.RoomId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update dungeon room {RoomId}", room.RoomId);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var room = await context.DungeonRooms.SingleOrDefaultAsync(r => r.RoomId == roomId, ct);
            if (room is not null)
            {
                context.DungeonRooms.Remove(room);
                await context.SaveChangesAsync(ct);
            }

            await DeleteDungeonRoomCacheAsync(roomId);

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete dungeon room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, CancellationToken ct = default)
    {
        try
        {
            var room = await GetByIdAsync(roomId, ct);
            if (room is null)
                return JoinRoomAtomicResult.RoomNotFound;

            return room.Status == RoomStatus.Waiting
                ? JoinRoomAtomicResult.Success
                : JoinRoomAtomicResult.InvalidStatus;
        }
        catch (KeyNotFoundException)
        {
            return JoinRoomAtomicResult.RoomNotFound;
        }
    }

    private async Task SetDungeonRoomCacheAsync(Domain.Entities.DungeonRoom room)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.DungeonRoom(room.RoomId),
        [
            new HashEntry("RoomId", room.RoomId),
            new HashEntry("RoomName", room.RoomName),
            new HashEntry("HostUserId", room.HostUserId),
            new HashEntry("MaxPlayers", room.MaxPlayers),
            new HashEntry("MapId", room.MapId),
            new HashEntry("Status", room.Status.ToString()),
            new HashEntry("CreatedAt", room.CreatedAt.ToString("O")),
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.DungeonRoom(room.RoomId), RedisSettings.RedisCacheTtl);

        if (room.Status != RoomStatus.Closed)
        {
            _ = transaction.SetAddAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
            _ = transaction.KeyExpireAsync(RedisKeys.DungeonRoomActive(), RedisSettings.RedisCacheTtl);
        }
        else
        {
            _ = transaction.SetRemoveAsync(RedisKeys.DungeonRoomActive(), room.RoomId);
        }

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set dungeon room cache");
    }

    public Task InvalidateCacheAsync(long roomId, CancellationToken ct = default)
        => DeleteDungeonRoomCacheAsync(roomId);

    private async Task DeleteDungeonRoomCacheAsync(long roomId)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(RedisKeys.DungeonRoom(roomId));
            _ = transaction.SetRemoveAsync(RedisKeys.DungeonRoomActive(), roomId);

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to delete dungeon room cache");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete dungeon room cache for {RoomId}", roomId);
            throw;
        }
    }

    private Domain.Entities.DungeonRoom? ParseDungeonRoomFromRedis(
        long roomId,
        HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("RoomId", out var roomIdStr) ||
            !dict.TryGetValue("RoomName", out var roomName) ||
            !dict.TryGetValue("HostUserId", out var hostUserIdStr) ||
            !dict.TryGetValue("MaxPlayers", out var maxPlayersStr) ||
            !dict.TryGetValue("Status", out var statusStr) ||
            !dict.TryGetValue("CreatedAt", out var createdAtStr))
        {
            logger.LogWarning("Dungeon room {RoomId} has missing fields", roomId);
            return null;
        }

        if (!long.TryParse(roomIdStr, out var id))
        {
            logger.LogWarning("Invalid dungeon room id value {RoomIdValue}", roomIdStr);
            return null;
        }
    
        if (!long.TryParse(hostUserIdStr, out var hostUserId))
        {
            logger.LogWarning("Invalid host user id value {HostUserIdValue}", hostUserIdStr);
            return null;
        }
    
        if (!int.TryParse(maxPlayersStr, out var maxPlayers))
        {
            logger.LogWarning("Invalid max players value {MaxPlayersValue}", maxPlayersStr);
            return null;
        }
    
        if (!Enum.TryParse<RoomStatus>(statusStr, out var status))
        {
            logger.LogWarning("Invalid dungeon room status value {StatusValue}", statusStr);
            return null;
        }
    
        if (!DateTime.TryParse(createdAtStr, null, DateTimeStyles.RoundtripKind, out var createdAt))
        {
            logger.LogWarning("Invalid dungeon room created-at value {CreatedAtValue}", createdAtStr);
            return null;
        }

        // MapId 는 필수 검증 제외 — 없는 옛 캐시는 기본 맵으로 폴백한다(거부하지 않음).
        var mapId = dict.TryGetValue("MapId", out var mapIdStr) && !string.IsNullOrEmpty(mapIdStr)
            ? mapIdStr
            : Shared.Infrastructure.Spawn.MapIds.Default;

        return Domain.Entities.DungeonRoom.FromRedis(
            id,
            roomName,
            hostUserId,
            maxPlayers,
            mapId,
            status,
            createdAt);
    }
}
