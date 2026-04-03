using System.Globalization;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Domain.Entities.GameSession;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.GameSession;

public class GameSessionPlayerRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<GameSessionPlayerRepository> logger) : IGameSessionPlayerRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string GameSessionPlayerKey = "game:session:player";
    private const string SessionPlayerMappingKey = "game:session:player:by-session";
    private const string UserPlayerMappingKey = "game:session:player:by-user";

    public async Task<GameSessionPlayer> CreateAsync(long gameSessionId, long userId, CancellationToken ct = default)
    {
        try
        {
            var gameSessionPlayer = GameSessionPlayer.Create(gameSessionId, userId);
            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync(GetPlayerKey(gameSessionId, userId),
            [
                new HashEntry("GameSessionId", gameSessionPlayer.GameSessionId),
                new HashEntry("UserId", gameSessionPlayer.UserId),
                new HashEntry("JoinedAt", gameSessionPlayer.JoinedAt.ToString("O"))
            ]);
            _ = transaction.SetAddAsync($"{SessionPlayerMappingKey}:{gameSessionId}", userId);
            _ = transaction.StringSetAsync($"{UserPlayerMappingKey}:{userId}", gameSessionId);

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create game session player: transaction rolled back");

            return gameSessionPlayer;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create game session player for session {GameSessionId} and user {UserId}", gameSessionId, userId);
            throw;
        }
    }

    public async Task<List<GameSessionPlayer>> GetPlayersByGameSessionIdAsync(long gameSessionId, CancellationToken ct = default)
    {
        try
        {
            var userIdValues = await _database.SetMembersAsync($"{SessionPlayerMappingKey}:{gameSessionId}");
            if (userIdValues.Length == 0)
                return [];

            var players = new List<GameSessionPlayer>(userIdValues.Length);
            foreach (var userIdValue in userIdValues)
            {
                if (!long.TryParse(userIdValue.ToString(), out var userId))
                {
                    logger.LogWarning("Invalid game session player mapping for session {GameSessionId}: {UserIdValue}", gameSessionId, userIdValue);
                    continue;
                }

                var player = await GetAsync(gameSessionId, userId);
                if (player is not null)
                    players.Add(player);
            }

            return players;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get game session player by session {GameSessionId}", gameSessionId);
            throw;
        }
    }

    public async Task<GameSessionPlayer?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var gameSessionIdValue = await _database.StringGetAsync($"{UserPlayerMappingKey}:{userId}");
            if (!gameSessionIdValue.HasValue || gameSessionIdValue.IsNullOrEmpty)
                return null;

            if (!long.TryParse(gameSessionIdValue.ToString(), out var gameSessionId))
            {
                logger.LogWarning("Invalid game session player mapping for user {UserId}: {GameSessionIdValue}", userId, gameSessionIdValue);
                return null;
            }

            return await GetAsync(gameSessionId, userId);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get game session player by user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(GameSessionPlayer gameSessionPlayer, CancellationToken ct = default)
    {
        try
        {
            if (gameSessionPlayer.GameSessionId <= 0 || gameSessionPlayer.UserId <= 0)
                throw new InvalidOperationException("UpdateAsync requires an existing game session player");

            var existingPlayer = await GetAsync(gameSessionPlayer.GameSessionId, gameSessionPlayer.UserId);
            if (existingPlayer is null)
                return false;

            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync(GetPlayerKey(gameSessionPlayer.GameSessionId, gameSessionPlayer.UserId),
            [
                new HashEntry("GameSessionId", gameSessionPlayer.GameSessionId),
                new HashEntry("UserId", gameSessionPlayer.UserId),
                new HashEntry("JoinedAt", gameSessionPlayer.JoinedAt.ToString("O"))
            ]);
            _ = transaction.SetAddAsync($"{SessionPlayerMappingKey}:{gameSessionPlayer.GameSessionId}", gameSessionPlayer.UserId);
            _ = transaction.StringSetAsync($"{UserPlayerMappingKey}:{gameSessionPlayer.UserId}", gameSessionPlayer.GameSessionId);

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update game session player for session {GameSessionId} and user {UserId}", gameSessionPlayer.GameSessionId, gameSessionPlayer.UserId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long gameSessionId, long userId, CancellationToken ct = default)
    {
        try
        {
            var existingPlayer = await GetAsync(gameSessionId, userId);
            if (existingPlayer is null)
                return false;

            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(GetPlayerKey(gameSessionId, userId));
            _ = transaction.SetRemoveAsync($"{SessionPlayerMappingKey}:{gameSessionId}", userId);
            _ = transaction.KeyDeleteAsync($"{UserPlayerMappingKey}:{userId}");

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove game session player for session {GameSessionId} and user {UserId}", gameSessionId, userId);
            throw;
        }
    }

    private async Task<GameSessionPlayer?> GetAsync(long gameSessionId, long userId)
    {
        var entries = await _database.HashGetAllAsync(GetPlayerKey(gameSessionId, userId));
        if (entries.Length == 0)
            return null;

        return ParseGameSessionPlayer(gameSessionId, userId, entries);
    }

    private GameSessionPlayer? ParseGameSessionPlayer(long gameSessionId, long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("GameSessionId", out var gameSessionIdStr) ||
            !dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("JoinedAt", out var joinedAtStr))
        {
            logger.LogWarning("Game session player {GameSessionId}:{UserId} has missing fields", gameSessionId, userId);
            return null;
        }

        if (!long.TryParse(gameSessionIdStr, out var parsedGameSessionId))
        {
            logger.LogWarning("Failed to parse game session ID for player: {GameSessionId}", gameSessionIdStr);
            return null;
        }

        if (!long.TryParse(userIdStr, out var parsedUserId))
        {
            logger.LogWarning("Failed to parse user ID for game session player: {UserId}", userIdStr);
            return null;
        }

        if (!DateTime.TryParse(joinedAtStr, null, DateTimeStyles.RoundtripKind, out var joinedAt))
        {
            logger.LogWarning("Failed to parse joined at for game session player: {JoinedAt}", joinedAtStr);
            return null;
        }

        return GameSessionPlayer.FromRedis(parsedGameSessionId, parsedUserId, joinedAt);
    }

    private static string GetPlayerKey(long gameSessionId, long userId)
    {
        return $"{GameSessionPlayerKey}:{gameSessionId}:{userId}";
    }
}
