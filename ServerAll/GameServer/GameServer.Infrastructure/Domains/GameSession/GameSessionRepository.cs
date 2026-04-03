using System.Globalization;
using GameSessionEntity = GameServer.Domain.Entities.GameSession.GameSession;

using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Domain.Entities.GameSession;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.GameSession;

public class GameSessionRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<GameSessionRepository> logger) : IGameSessionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    
    private const string GameSessionKey = "game:gamesession";
    private const string ActiveSessionsKey = "game:gamesession:active";
    private const string RoomSessionMappingKey = "game:gamesession:by-room";
    private const string GameSessionCounterKey = "game:gamesession:id:counter";
    
    public async Task<GameSessionEntity> CreateAsync(long roomId, string socketIp, int socketPort, CancellationToken ct = default)
    {
        try
        {
            var gameSession = GameSessionEntity.Create(roomId, socketIp, socketPort);
            
            var gameSessionId = await _database.StringIncrementAsync(GameSessionCounterKey);
            gameSession.SetId(gameSessionId);
            
            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync($"{GameSessionKey}:{gameSessionId}",
            [
                new HashEntry("GameSessionId", gameSession.GameSessionId),
                new HashEntry("RoomId", gameSession.RoomId),
                new HashEntry("SocketIp", gameSession.SocketIp),
                new HashEntry("SocketPort", gameSession.SocketPort),
                new HashEntry("StartedAt", gameSession.StartedAt.ToString("O")),
                new HashEntry("EndedAt", RedisValue.Null),
                new HashEntry("Status", gameSession.Status.ToString())
            ]);
            _ = transaction.SetAddAsync(ActiveSessionsKey, gameSession.GameSessionId);
            _ = transaction.StringSetAsync($"{RoomSessionMappingKey}:{gameSession.RoomId}", gameSession.GameSessionId);
            
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create game session: transaction rolled back");
            
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
            var entries = await _database.HashGetAllAsync($"{GameSessionKey}:{gameSessionId}");
            if (entries.Length == 0)
                return null;

            return ParseGameSession(gameSessionId, entries);
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
            var gameSessionIdValue = await _database.StringGetAsync($"{RoomSessionMappingKey}:{roomId}");
            if (!gameSessionIdValue.HasValue || gameSessionIdValue.IsNullOrEmpty)
                return null;

            if (!long.TryParse(gameSessionIdValue.ToString(), out var gameSessionId))
            {
                logger.LogWarning("Invalid game session mapping for room {RoomId}: {GameSessionIdValue}", roomId, gameSessionIdValue);
                return null;
            }

            return await GetAsync(gameSessionId, ct);
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

            var existingGameSession = await GetAsync(gameSession.GameSessionId, ct);
            if (existingGameSession is null)
                return false;

            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync($"{GameSessionKey}:{gameSession.GameSessionId}",
            [
                new HashEntry("GameSessionId", gameSession.GameSessionId),
                new HashEntry("RoomId", gameSession.RoomId),
                new HashEntry("SocketIp", gameSession.SocketIp),
                new HashEntry("SocketPort", gameSession.SocketPort),
                new HashEntry("StartedAt", gameSession.StartedAt.ToString("O")),
                new HashEntry(
                    "EndedAt",
                    gameSession.EndedAt.HasValue ? gameSession.EndedAt.Value.ToString("O") : RedisValue.Null),
                new HashEntry("Status", gameSession.Status.ToString())
            ]);

            if (gameSession.Status == GameSessionStatus.Ended)
            {
                _ = transaction.SetRemoveAsync(ActiveSessionsKey, gameSession.GameSessionId);
                _ = transaction.KeyDeleteAsync($"{RoomSessionMappingKey}:{gameSession.RoomId}");
            }
            else
            {
                _ = transaction.SetAddAsync(ActiveSessionsKey, gameSession.GameSessionId);
                _ = transaction.StringSetAsync($"{RoomSessionMappingKey}:{gameSession.RoomId}", gameSession.GameSessionId);
            }

            return await transaction.ExecuteAsync();
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
            var gameSession = await GetAsync(gameSessionId, ct);
            if (gameSession is null)
                return false;

            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync($"{GameSessionKey}:{gameSessionId}");
            _ = transaction.SetRemoveAsync(ActiveSessionsKey, gameSessionId);
            _ = transaction.KeyDeleteAsync($"{RoomSessionMappingKey}:{gameSession.RoomId}");

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove game session {GameSessionId}", gameSessionId);
            throw;
        }
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
