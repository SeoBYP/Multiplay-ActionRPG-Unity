using System.Globalization;
using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Domain.Entities.GameSession;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.GameSession;

public class GameSessionPlayerRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<GameSessionPlayerRepository> logger) : IGameSessionPlayerRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<GameSessionPlayer> CreateAsync(long gameSessionId, long userId, CancellationToken ct = default)
    {
        try
        {
            var gameSessionPlayer = GameSessionPlayer.Create(gameSessionId, userId);

            var entry = await context.GameSessionPlayers.AddAsync(gameSessionPlayer, ct);
            await context.SaveChangesAsync(ct);

            var player = entry.Entity;
            await SetGameSessionPlayerCacheAsync(player);

            return player;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create game session player for session {GameSessionId} and user {UserId}",
                gameSessionId, userId);
            throw;
        }
    }

    public async Task<List<GameSessionPlayer>> GetPlayersByGameSessionIdAsync(long gameSessionId,
        CancellationToken ct = default)
    {
        try
        {
            var userIdValues = await _database.SetMembersAsync(RedisKeys.GameSessionPlayerBySession(gameSessionId));
            if (userIdValues.Length > 0)
            {
                var players = new List<GameSessionPlayer>(userIdValues.Length);
                foreach (var userIdValue in userIdValues)
                {
                    if (!long.TryParse(userIdValue.ToString(), out var userId))
                    {
                        logger.LogWarning("Invalid game session player mapping for session {GameSessionId}: {UserIdValue}",
                            gameSessionId, userIdValue);
                        continue;
                    }

                    var player = await GetAsync(gameSessionId, userId, ct);
                    if (player is not null)
                        players.Add(player);
                }

                if (players.Count == userIdValues.Length)
                    return players;
            }

            var dbPlayers = await context.GameSessionPlayers
                .Where(gsp => gsp.GameSessionId == gameSessionId)
                .ToListAsync(ct);

            var tasks = dbPlayers.Select(async player => await SetGameSessionPlayerCacheAsync(player));
            await Task.WhenAll(tasks);

            return dbPlayers;
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
            var gameSessionIdValue = await _database.StringGetAsync(RedisKeys.GameSessionPlayerByUser(userId));
            if (gameSessionIdValue.HasValue && !gameSessionIdValue.IsNullOrEmpty)
            {
                if (long.TryParse(gameSessionIdValue.ToString(), out var gameSessionId))
                {
                    return await GetAsync(gameSessionId, userId, ct);
                }
            }

            var dbPlayer = await context.GameSessionPlayers.SingleOrDefaultAsync(gsp => gsp.UserId == userId, ct);
            if (dbPlayer is not null)
            {
                await SetGameSessionPlayerCacheAsync(dbPlayer);
            }

            return dbPlayer;
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

            var existingPlayer = await context.GameSessionPlayers
                .AsNoTracking()
                .SingleOrDefaultAsync(gsp =>
                    gsp.GameSessionId == gameSessionPlayer.GameSessionId && gsp.UserId == gameSessionPlayer.UserId, ct);
            if (existingPlayer is null)
                return false;

            context.GameSessionPlayers.Update(gameSessionPlayer);
            await context.SaveChangesAsync(ct);

            await DeleteGameSessionPlayerCacheAsync(gameSessionPlayer.GameSessionId, gameSessionPlayer.UserId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update game session player for session {GameSessionId} and user {UserId}",
                gameSessionPlayer.GameSessionId, gameSessionPlayer.UserId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long gameSessionId, long userId, CancellationToken ct = default)
    {
        try
        {
            var dbPlayer = await context.GameSessionPlayers
                .SingleOrDefaultAsync(gsp => gsp.GameSessionId == gameSessionId && gsp.UserId == userId, ct);
            if (dbPlayer is not null)
            {
                context.GameSessionPlayers.Remove(dbPlayer);
                await context.SaveChangesAsync(ct);
            }

            await DeleteGameSessionPlayerCacheAsync(gameSessionId, userId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove game session player for session {GameSessionId} and user {UserId}",
                gameSessionId, userId);
            throw;
        }
    }

    private async Task<GameSessionPlayer?> GetAsync(long gameSessionId, long userId, CancellationToken ct = default)
    {
        var entries = await _database.HashGetAllAsync(RedisKeys.GameSessionPlayer(gameSessionId, userId));
        if (entries.Length > 0)
            return ParseGameSessionPlayer(gameSessionId, userId, entries);

        var dbPlayer = await context.GameSessionPlayers
            .SingleOrDefaultAsync(gsp => gsp.GameSessionId == gameSessionId && gsp.UserId == userId, ct);

        if (dbPlayer is not null)
        {
            await SetGameSessionPlayerCacheAsync(dbPlayer);
        }

        return dbPlayer;
    }

    private async Task SetGameSessionPlayerCacheAsync(GameSessionPlayer player)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.GameSessionPlayer(player.GameSessionId, player.UserId),
        [
            new HashEntry("GameSessionId", player.GameSessionId),
            new HashEntry("UserId", player.UserId),
            new HashEntry("JoinedAt", player.JoinedAt.ToString("O"))
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.GameSessionPlayer(player.GameSessionId, player.UserId), RedisSettings.RedisCacheTtl);

        _ = transaction.SetAddAsync(RedisKeys.GameSessionPlayerBySession(player.GameSessionId), player.UserId);
        _ = transaction.KeyExpireAsync(RedisKeys.GameSessionPlayerBySession(player.GameSessionId), RedisSettings.RedisCacheTtl);

        _ = transaction.StringSetAsync(RedisKeys.GameSessionPlayerByUser(player.UserId), player.GameSessionId,
            RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set game session player cache");
    }

    private async Task DeleteGameSessionPlayerCacheAsync(long gameSessionId, long userId)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(RedisKeys.GameSessionPlayer(gameSessionId, userId));
            _ = transaction.SetRemoveAsync(RedisKeys.GameSessionPlayerBySession(gameSessionId), userId);
            _ = transaction.KeyDeleteAsync(RedisKeys.GameSessionPlayerByUser(userId));

            var commited = await transaction.ExecuteAsync();
            if (!commited)
                throw new InvalidOperationException("Failed to delete game session player cache");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete game session player cache for {GameSessionId}:{UserId}", gameSessionId, userId);
            throw;
        }
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
}
