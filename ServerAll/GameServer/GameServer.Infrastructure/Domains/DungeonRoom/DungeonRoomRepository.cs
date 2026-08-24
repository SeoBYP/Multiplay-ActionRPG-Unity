using System.Globalization;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

/// <summary>
/// PostgreSQL(DB) + Redis 캐시 기반 던전 방 저장소 구현체
/// </summary>
public class DungeonRoomRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<DungeonRoomRepository> logger) : IDungeonRoomRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    /// <summary>
    /// 새로운 던전 방을 생성하고 DB와 Redis에 저장합니다.
    /// </summary>
    public async Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4,
        string mapId = "", CancellationToken ct = default)
    {
        try
        {
            // 1. 도메인 모델 생성 (mapId 정규화·검증은 Application 책임 — 여기선 받은 값을 그대로 보관)
            var room = Domain.Entities.DungeonRoom.Create(roomName, hostId, maxPlayers, mapId);

            // 2. DB 저장
            var entry = await context.DungeonRooms.AddAsync(room, ct);
            await context.SaveChangesAsync(ct);

            var createdRoom = entry.Entity;

            // 3. Redis 캐시 설정
            await SetDungeonRoomCacheAsync(createdRoom);

            return createdRoom;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room for host {HostUserId}", hostId);
            throw;
        }
    }

    /// <summary>
    /// 방 고유 ID를 사용하여 던전 방 정보를 상세 조회합니다. (플레이어 목록 포함)
    /// </summary>
    public async Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.DungeonRoom(roomId));
            if (entries.Length > 0)
                return ParseDungeonRoomFromRedis(roomId, entries);

            // AsNoTracking 필수: SubscribeRoom 스트리밍 RPC는 DbContext(Scoped)를 수십 초 유지한다.
            // 추적 쿼리면 EF identity map이 먼저 적재된 stale 엔티티(예: Starting)를 그대로 돌려주고
            // DB 최신값(Playing)으로 갱신하지 않아, 다른 스코프가 쓴 변경을 영원히 못 읽는다.
            // 이 리포지토리는 cache-aside 읽기 전용이므로 추적이 불필요하다.
            var room = await context.DungeonRooms.AsNoTracking().SingleOrDefaultAsync(r => r.RoomId == roomId, ct);
            if (room is null)
                return null; // 없는 방은 null 반환(선언이 Task<DungeonRoom?>). 모든 호출자가 if(room is null)로 처리한다.

            await SetDungeonRoomCacheAsync(room);
            return room;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 사용자 ID를 사용하여 해당 사용자가 현재 참여 중인 던전 방 정보를 조회합니다.
    /// </summary>
    public async Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            // DungeonRoomPlayer를 통해 참여 중인 방 ID를 먼저 찾고 방 정보를 조회
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

    /// <summary>
    /// 현재 서버에 존재하는 모든 활성 던전 방 목록을 조회합니다.
    /// </summary>
    public async Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default)
    {
        try
        {
            // 목록의 진실은 DB 다. `room:active` 집합을 근거로 삼지 않는다 —
            // 그 집합은 항목별이 아니라 **집합 전체**에 TTL 이 걸려 있어(SetDungeonRoomCacheAsync)
            // 만료 뒤 최근 접근분만 재적재된다. 멤버십이 수시로 흔들리므로 목록이 그걸 믿으면
            // ① DB 에 살아 있는 방이 통째로 사라지고
            // ② 연속 호출이 서로 다른 집합을 봐서 페이징에서 같은 방이 두 페이지에 나온다.
            // (전량 조회 자체의 성능 한계는 별건 — 백로그 B4 의 DB 페이징 푸시다운)
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
            // B4: 전량을 읽어 메모리에서 자르지 않는다. 정렬·건너뛰기·자르기를 전부 DB 가 한다.
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

    /// <summary>
    /// 현재 활성화된 던전 방의 총 개수를 조회합니다.
    /// </summary>
    public async Task<long> GetActiveRoomCountAsync(CancellationToken ct = default)
    {
        // 목록과 같은 이유로 `room:active` 크기를 세지 않는다 — 집합 전체 TTL 때문에
        // 멤버십이 흔들려 실제보다 훨씬 작은 수를 "참"으로 돌려준다.
        return await context.DungeonRooms.CountAsync(r => r.Status != RoomStatus.Closed, ct);
    }

    /// <summary>
    /// 던전 방의 상태(이름, 방장, 인원, 상태 등) 및 플레이어 목록을 업데이트합니다.
    /// </summary>
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

    /// <summary>
    /// Redis에서 조회한 Hash 데이터와 Set 데이터를 사용하여 DungeonRoom 도메인 객체로 변환합니다.
    /// </summary>
    private Domain.Entities.DungeonRoom? ParseDungeonRoomFromRedis(
        long roomId,
        HashEntry[] entries)
    {
        // 1. Hash를 Dictionary로 변환
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        // 2. 필수 필드 검증
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

        // 3. 파싱
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

        // MapId 는 필수 검증에서 제외: 4.3 이전에 캐시된 방엔 없을 수 있어 기본 맵으로 폴백한다(거부하지 않음).
        var mapId = dict.TryGetValue("MapId", out var mapIdStr) && !string.IsNullOrEmpty(mapIdStr)
            ? mapIdStr
            : Shared.Infrastructure.Spawn.MapIds.Default;

        // 4. FromRedis로 DungeonRoom 재구성
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
