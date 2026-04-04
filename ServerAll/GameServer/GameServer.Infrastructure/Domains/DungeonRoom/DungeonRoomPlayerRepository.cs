using System.Globalization;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class DungeonRoomPlayerRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<DungeonRoomPlayerRepository> logger) : IDungeonRoomPlayerRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string DungeonRoomPlayerKey = "game:room:player";
    private const string RoomPlayerMappingKey = "game:room:player:by-room";
    private const string UserPlayerMappingKey = "game:room:player:by-user";

    public async Task<DungeonRoomPlayer> CreateAsync(long roomId, long userId, CancellationToken ct = default)
    {
        try
        {
            var player = DungeonRoomPlayer.Create(roomId, userId);
            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync(GetPlayerKey(roomId, userId),
            [
                new HashEntry("RoomId", player.RoomId),
                new HashEntry("UserId", player.UserId),
                new HashEntry("JoinedAt", player.JoinedAt.ToString("O"))
            ]);
            _ = transaction.SetAddAsync($"{RoomPlayerMappingKey}:{roomId}", userId);
            _ = transaction.StringSetAsync($"{UserPlayerMappingKey}:{userId}", roomId);

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create dungeon room player: transaction rolled back");

            return player;
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
            var userIdValues = await _database.SetMembersAsync($"{RoomPlayerMappingKey}:{roomId}");
            if (userIdValues.Length == 0)
                return [];

            var players = new List<DungeonRoomPlayer>(userIdValues.Length);
            foreach (var userIdValue in userIdValues)
            {
                if (!long.TryParse(userIdValue.ToString(), out var userId))
                {
                    logger.LogWarning("Invalid dungeon room player mapping for room {RoomId}: {UserIdValue}", roomId, userIdValue);
                    continue;
                }

                var player = await GetAsync(roomId, userId);
                if (player is not null)
                    players.Add(player);
            }

            return players;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room players for room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<DungeonRoomPlayer?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var roomIdValue = await _database.StringGetAsync($"{UserPlayerMappingKey}:{userId}");
            if (!roomIdValue.HasValue || roomIdValue.IsNullOrEmpty)
                return null;

            if (!long.TryParse(roomIdValue.ToString(), out var roomId))
            {
                logger.LogWarning("Invalid dungeon room player mapping for user {UserId}: {RoomIdValue}", userId, roomIdValue);
                return null;
            }

            return await GetAsync(roomId, userId);
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
            var existingPlayer = await GetAsync(roomId, userId);
            if (existingPlayer is null)
                return false;

            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync(GetPlayerKey(roomId, userId));
            _ = transaction.SetRemoveAsync($"{RoomPlayerMappingKey}:{roomId}", userId);
            _ = transaction.KeyDeleteAsync($"{UserPlayerMappingKey}:{userId}");

            return await transaction.ExecuteAsync();
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
            var players = await GetPlayersByRoomIdAsync(roomId, ct);
            if (players.Count == 0)
                return false;

            var transaction = _database.CreateTransaction();

            foreach (var player in players)
            {
                _ = transaction.KeyDeleteAsync(GetPlayerKey(roomId, player.UserId));
                _ = transaction.KeyDeleteAsync($"{UserPlayerMappingKey}:{player.UserId}");
            }

            _ = transaction.KeyDeleteAsync($"{RoomPlayerMappingKey}:{roomId}");

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove dungeon room players for room {RoomId}", roomId);
            throw;
        }
    }

    private async Task<DungeonRoomPlayer?> GetAsync(long roomId, long userId)
    {
        var entries = await _database.HashGetAllAsync(GetPlayerKey(roomId, userId));
        if (entries.Length == 0)
            return null;

        return ParseDungeonRoomPlayer(roomId, userId, entries);
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

    private static string GetPlayerKey(long roomId, long userId)
    {
        return $"{DungeonRoomPlayerKey}:{roomId}:{userId}";
    }
}
