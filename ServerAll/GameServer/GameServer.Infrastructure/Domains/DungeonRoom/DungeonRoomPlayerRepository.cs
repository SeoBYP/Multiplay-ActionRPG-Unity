using System.Globalization;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class DungeonRoomPlayerRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<DungeonRoomPlayerRepository> logger) : IDungeonRoomPlayerRepository
{
    /// <summary>PostgreSQL unique_violation SQLSTATE.</summary>
    private const string UniqueViolation = "23505";

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<DungeonRoomPlayer> CreateAsync(long roomId, long userId, CancellationToken ct = default)
    {
        try
        {
            var dungeonRoomPlayer = DungeonRoomPlayer.Create(roomId, userId);

            var entry = await context.DungeonRoomPlayers.AddAsync(dungeonRoomPlayer, ct);
            await context.SaveChangesAsync(ct);

            var player = entry.Entity;
            await SetDungeonRoomPlayerCacheAsync(player);

            return player;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            // UserId UNIQUE 제약 = "한 유저는 한 방만"의 최종 방어선.
            // DB 예외 타입이 Application 까지 새지 않도록 도메인 예외로 번역한다.
            context.ChangeTracker.Clear();
            logger.LogWarning(e, "User {UserId} is already in a room — rejected join to room {RoomId}", userId, roomId);
            throw new PlayerAlreadyInRoomException(userId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room player for room {RoomId} and user {UserId}", roomId, userId);
            throw;
        }
    }

    public async Task<List<DungeonRoomPlayer>> GetPlayersByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var userIdValues = await _database.SetMembersAsync(RedisKeys.DungeonRoomPlayerByRoom(roomId));
            if (userIdValues.Length > 0)
            {
                var players = new List<DungeonRoomPlayer>(userIdValues.Length);
                foreach (var userIdValue in userIdValues)
                {
                    if (!long.TryParse(userIdValue.ToString(), out var userId))
                    {
                        logger.LogWarning("Invalid dungeon room player mapping for room {RoomId}: {UserIdValue}", roomId, userIdValue);
                        continue;
                    }

                    var player = await GetAsync(roomId, userId, ct);
                    if (player is not null)
                        players.Add(player);
                }

                if (players.Count == userIdValues.Length)
                    return players;
            }

            var dbPlayers = await context.DungeonRoomPlayers
                .AsNoTracking()
                .Where(drp => drp.RoomId == roomId)
                .ToListAsync(ct);

            var tasks = dbPlayers.Select(async player => await SetDungeonRoomPlayerCacheAsync(player));
            await Task.WhenAll(tasks);

            return dbPlayers;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room players for room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<List<DungeonRoomPlayer>> GetPlayersByRoomIdsAsync(IReadOnlyCollection<long> roomIds, CancellationToken ct = default)
    {
        if (roomIds.Count == 0)
            return [];

        try
        {
            // 방 목록 화면용 배치 읽기 — DB가 진실의 원천(CreateAsync가 DB 먼저 커밋).
            // 캐시-aside 대신 단일 AsNoTracking 쿼리로 N+1(방마다 2왕복)을 1쿼리로 축소.
            return await context.DungeonRoomPlayers
                .AsNoTracking()
                .Where(drp => roomIds.Contains(drp.RoomId))
                .ToListAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to batch-get dungeon room players for {RoomCount} rooms", roomIds.Count);
            throw;
        }
    }

    public async Task<DungeonRoomPlayer?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var roomIdValue = await _database.StringGetAsync(RedisKeys.DungeonRoomPlayerByUser(userId));
            if (roomIdValue.HasValue && !roomIdValue.IsNullOrEmpty)
            {
                if (long.TryParse(roomIdValue.ToString(), out var roomId))
                {
                    return await GetAsync(roomId, userId, ct);
                }
            }

            var dbPlayer = await context.DungeonRoomPlayers.AsNoTracking().SingleOrDefaultAsync(drp => drp.UserId == userId, ct);
            if (dbPlayer is not null)
            {
                await SetDungeonRoomPlayerCacheAsync(dbPlayer);
            }

            return dbPlayer;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room player by user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long roomId, long userId, CancellationToken ct = default)
    {
        try
        {
            var dbPlayer = await context.DungeonRoomPlayers
                .SingleOrDefaultAsync(drp => drp.RoomId == roomId && drp.UserId == userId, ct);
            if (dbPlayer is not null)
            {
                context.DungeonRoomPlayers.Remove(dbPlayer);
                await context.SaveChangesAsync(ct);
            }

            await DeleteDungeonRoomPlayerCacheAsync(roomId, userId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove dungeon room player for room {RoomId} and user {UserId}", roomId, userId);
            throw;
        }
    }

    public async Task<bool> RemoveByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var dbPlayers = await context.DungeonRoomPlayers
                .Where(drp => drp.RoomId == roomId)
                .ToListAsync(ct);

            if (dbPlayers.Count > 0)
            {
                context.DungeonRoomPlayers.RemoveRange(dbPlayers);
                await context.SaveChangesAsync(ct);
            }

            // dbPlayers 기준으로 직접 캐시 삭제
            var deleteTasks = dbPlayers.Select(p => DeleteDungeonRoomPlayerCacheAsync(roomId, p.UserId));
            await Task.WhenAll(deleteTasks);

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove dungeon room players for room {RoomId}", roomId);
            throw;
        }
    }

    private async Task<DungeonRoomPlayer?> GetAsync(long roomId, long userId, CancellationToken ct = default)
    {
        var entries = await _database.HashGetAllAsync(RedisKeys.DungeonRoomPlayer(roomId, userId));
        if (entries.Length > 0)
            return ParseDungeonRoomPlayer(roomId, userId, entries);

        var dbPlayer = await context.DungeonRoomPlayers
            .AsNoTracking()
            .SingleOrDefaultAsync(drp => drp.RoomId == roomId && drp.UserId == userId, ct);

        if (dbPlayer is not null)
        {
            await SetDungeonRoomPlayerCacheAsync(dbPlayer);
        }

        return dbPlayer;
    }

    private async Task SetDungeonRoomPlayerCacheAsync(DungeonRoomPlayer player)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.DungeonRoomPlayer(player.RoomId, player.UserId),
        [
            new HashEntry("RoomId", player.RoomId),
            new HashEntry("UserId", player.UserId),
            new HashEntry("JoinedAt", player.JoinedAt.ToString("O"))
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.DungeonRoomPlayer(player.RoomId, player.UserId), RedisSettings.RedisCacheTtl);

        _ = transaction.SetAddAsync(RedisKeys.DungeonRoomPlayerByRoom(player.RoomId), player.UserId);
        _ = transaction.KeyExpireAsync(RedisKeys.DungeonRoomPlayerByRoom(player.RoomId), RedisSettings.RedisCacheTtl);

        _ = transaction.StringSetAsync(RedisKeys.DungeonRoomPlayerByUser(player.UserId), player.RoomId,
            RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set dungeon room player cache");
    }

    private async Task DeleteDungeonRoomPlayerCacheAsync(long roomId, long userId)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(RedisKeys.DungeonRoomPlayer(roomId, userId));
            _ = transaction.SetRemoveAsync(RedisKeys.DungeonRoomPlayerByRoom(roomId), userId);
            _ = transaction.KeyDeleteAsync(RedisKeys.DungeonRoomPlayerByUser(userId));

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to delete dungeon room player cache");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete dungeon room player cache for {RoomId}:{UserId}", roomId, userId);
            throw;
        }
    }

    private DungeonRoomPlayer? ParseDungeonRoomPlayer(long roomId, long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("RoomId", out var roomIdStr) ||
            !dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("JoinedAt", out var joinedAtStr))
        {
            logger.LogWarning("Dungeon room player {RoomId}:{UserId} has missing fields", roomId, userId);
            return null;
        }

        if (!long.TryParse(roomIdStr, out var parsedRoomId))
        {
            logger.LogWarning("Failed to parse room ID for dungeon room player: {RoomId}", roomIdStr);
            return null;
        }

        if (!long.TryParse(userIdStr, out var parsedUserId))
        {
            logger.LogWarning("Failed to parse user ID for dungeon room player: {UserId}", userIdStr);
            return null;
        }

        if (!DateTime.TryParse(joinedAtStr, null, DateTimeStyles.RoundtripKind, out var joinedAt))
        {
            logger.LogWarning("Failed to parse joined at for dungeon room player: {JoinedAt}", joinedAtStr);
            return null;
        }

        return DungeonRoomPlayer.Restore(parsedRoomId, parsedUserId, joinedAt);
    }
}
