using System.Globalization;
using GameSessionEntity = GameServer.Domain.Entities.GameSession.GameSession;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Domain.Entities.GameSession;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.GameSession;

public class GameSessionRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<GameSessionRepository> logger) : IGameSessionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<GameSessionEntity> CreateAsync(long roomId, string socketIp, int socketPort,
        CancellationToken ct = default)
    {
        try
        {
            var newGameSession = GameSessionEntity.Create(roomId, socketIp, socketPort);

            var gameSessionEntry = await context.GameSessions.AddAsync(newGameSession, ct);
            await context.SaveChangesAsync(ct);

            var gameSession = gameSessionEntry.Entity;
            await SetGameSessionCacheAsync(gameSession);

            return gameSession;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create game session for room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<GameSessionEntity?> GetAsync(long gameSessionId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.GameSession(gameSessionId));
            if (entries.Length > 0)
                return ParseGameSession(gameSessionId, entries);

            var gameSession =
                await context.GameSessions.AsNoTracking().SingleOrDefaultAsync(gs => gs.GameSessionId == gameSessionId, ct);
            if (gameSession is null)
                throw new KeyNotFoundException($"Game session not found for game session id {gameSessionId}");

            await SetGameSessionCacheAsync(gameSession);
            return gameSession;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get game session {GameSessionId}", gameSessionId);
            throw;
        }
    }

    public async Task<GameSessionEntity?> GetByRoomIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var gameSessionIdValue = await _database.StringGetAsync(RedisKeys.GameSessionByRoom(roomId));
            if (gameSessionIdValue.HasValue)
            {
                if (long.TryParse(gameSessionIdValue.ToString(), out var gameSessionId))
                {
                    return await GetAsync(gameSessionId, ct);
                }
            }

            var gameSession = await context.GameSessions.AsNoTracking().SingleOrDefaultAsync(gs => gs.RoomId == roomId, ct);
            if (gameSession is null)
                return null;

            await SetGameSessionCacheAsync(gameSession);
            return gameSession;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get game session by room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(GameSessionEntity gameSession, CancellationToken ct = default)
    {
        try
        {
            if (gameSession.GameSessionId <= 0)
                throw new InvalidOperationException("UpdateAsync requires an existing game session");

            var existingGameSession = await context.GameSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(gs => gs.GameSessionId == gameSession.GameSessionId, ct);
            if (existingGameSession is null)
                throw new KeyNotFoundException(
                    $"Game session not found for game session id {gameSession.GameSessionId}");

            context.GameSessions.Update(gameSession);
            await context.SaveChangesAsync(ct);


            await DeleteGameSessionCacheAsync(gameSession.GameSessionId, gameSession.RoomId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update game session {GameSessionId}", gameSession.GameSessionId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long gameSessionId, CancellationToken ct = default)
    {
        try
        {
            var gameSession = await context.GameSessions.SingleOrDefaultAsync(gs => gs.GameSessionId == gameSessionId, cancellationToken: ct);
            if (gameSession is not null)
            {
                context.GameSessions.Remove(gameSession);
                await context.SaveChangesAsync(ct);
            }

            if (gameSession is null)
            {
                await DeleteGameSessionCacheAsync(gameSessionId);
            }
            else
            {
                await DeleteGameSessionCacheAsync(gameSessionId, gameSession.RoomId);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove game session {GameSessionId}", gameSessionId);
            throw;
        }
    }
    
    private async Task DeleteGameSessionCacheAsync(long gameSessionId, params long[] roomIds)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(RedisKeys.GameSession(gameSessionId));
            foreach (var roomId in roomIds.Distinct())
            {
                _ = transaction.KeyDeleteAsync(RedisKeys.GameSessionByRoom(roomId));
            }
            var commited = await transaction.ExecuteAsync();
            if (!commited)
                throw new InvalidOperationException("Failed to delete game session cache");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete game session cache for game session {0}", gameSessionId);
            throw;
        }
    }

    private async Task SetGameSessionCacheAsync(GameSessionEntity gameSession)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.GameSession(gameSession.GameSessionId),
        [
            new HashEntry("GameSessionId", gameSession.GameSessionId),
            new HashEntry("RoomId", gameSession.RoomId),
            new HashEntry("SocketIp", gameSession.SocketIp),
            new HashEntry("SocketPort", gameSession.SocketPort),
            new HashEntry("StartedAt", gameSession.StartedAt.ToString("O")),
            new HashEntry("EndedAt", gameSession.EndedAt?.ToString("O") ?? string.Empty),
            new HashEntry("Status", gameSession.Status.ToString())
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.GameSession(gameSession.GameSessionId), RedisSettings.RedisCacheTtl);

        _ = transaction.StringSetAsync(RedisKeys.GameSessionByRoom(gameSession.RoomId), gameSession.GameSessionId,
            RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to create game session: transaction rolled back");
    }

    private GameSessionEntity? ParseGameSession(long gameSessionId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("GameSessionId", out var gameSessionIdStr) ||
            !dict.TryGetValue("RoomId", out var roomIdStr) ||
            !dict.TryGetValue("SocketIp", out var socketIpStr) ||
            !dict.TryGetValue("SocketPort", out var socketPortStr) ||
            !dict.TryGetValue("StartedAt", out var startedAtStr) ||
            !dict.TryGetValue("Status", out var statusStr))
        {
            logger.LogWarning("Game session {GameSessionId} has missing fields", gameSessionId);
            return null;
        }

        if (!long.TryParse(gameSessionIdStr, out var id))
        {
            logger.LogWarning("Failed to parse game session ID: {GameSessionId}", gameSessionIdStr);
            return null;
        }

        if (!long.TryParse(roomIdStr, out var roomId))
        {
            logger.LogWarning("Failed to parse room ID: {RoomId}", roomIdStr);
            return null;
        }

        if (!int.TryParse(socketPortStr, out var socketPort))
        {
            logger.LogWarning("Failed to parse socket port: {SocketPort}", socketPortStr);
            return null;
        }

        if (!DateTime.TryParse(startedAtStr, null, DateTimeStyles.RoundtripKind, out var startedAt))
        {
            logger.LogWarning("Failed to parse started at: {StartedAt}", startedAtStr);
            return null;
        }

        DateTime? endedAt = null;
        if (dict.TryGetValue("EndedAt", out var endedAtStr) && !string.IsNullOrWhiteSpace(endedAtStr))
        {
            if (!DateTime.TryParse(endedAtStr, null, DateTimeStyles.RoundtripKind, out var parsedEndedAt))
            {
                logger.LogWarning("Failed to parse ended at: {EndedAt}", endedAtStr);
                return null;
            }

            endedAt = parsedEndedAt;
        }

        if (!Enum.TryParse<GameSessionStatus>(statusStr, out var status))
        {
            logger.LogWarning("Failed to parse game session status: {Status}", statusStr);
            return null;
        }

        return GameSessionEntity.FromRedis(
            id,
            roomId,
            socketIpStr,
            socketPort,
            startedAt,
            endedAt,
            status);
    }
}