using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

using User = Domain.Entities.User.User;

/// <summary>
/// 데이터베이스 연동 사용자 저장소 구현체
/// </summary>
public class UserRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<UserRepository> logger) : IUserRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string UserKey = "game:user";
    private const string UserPublicIdMappingKey = "game:user:publicid";

    /// <summary>
    /// 새로운 사용자를 추가합니다. (회원가입)
    /// </summary>
    public async Task<User> CreateAsync(CancellationToken ct = default)
    {
        try
        {
            var newUser = User.Create();

            var userEntry = await context.Users.AddAsync(newUser, ct);
            await context.SaveChangesAsync(ct);

            var user = userEntry.Entity;
            await SetUserCacheAsync(user);

            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add user profile");
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var user = await context.Users.SingleOrDefaultAsync(u => u.UserId == userId, ct);
            if (user is not null)
            {
                context.Users.Remove(user);
                await context.SaveChangesAsync(ct);
            }

            if (user is null)
            {
                await DeleteUserCacheAsync(userId);
            }
            else
            {
                await DeleteUserCacheAsync(userId, user.PublicId);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(User user, CancellationToken ct = default)
    {
        try
        {
            if (user.UserId <= 0)
                throw new InvalidOperationException("Invalid user id");

            var existingUser = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.UserId == user.UserId, ct);
            if (existingUser is null)
                throw new KeyNotFoundException($"User not found for user id {user.UserId}");

            context.Users.Update(user);
            await context.SaveChangesAsync(ct);

            // PublicId가 변경된 경우 이전/신규 매핑 키를 모두 비워 다음 조회에서 DB 기준으로 재구성한다.
            await DeleteUserCacheAsync(user.UserId, existingUser.PublicId, user.PublicId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update user profile {UserId}", user.UserId);
            throw;
        }
    }

    public async Task<User?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{UserKey}:{userId}");
            if (entries.Length > 0)
                return ParseUserFromRedis(userId, entries);

            var user = await context.Users.SingleOrDefaultAsync(u => u.UserId == userId, ct);
            if (user is null)
                throw new KeyNotFoundException($"User not found for user id {userId}");

            await SetUserCacheAsync(user);
            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user by id {UserId}", userId);
            throw;
        }
    }

    public async Task<List<User>> GetByIdsAsync(List<long> userIds, CancellationToken ct = default)
    {
        try
        {
            if (userIds.Count == 0)
                return [];

            var batch = _database.CreateBatch();
            var userEntries = userIds
                .Select(userId => batch.HashGetAllAsync($"{UserKey}:{userId}"))
                .ToList();

            batch.Execute();

            var usersById = new Dictionary<long, User>();
            var missedUserIds = new List<long>();

            for (int i = 0; i < userIds.Count; i++)
            {
                var userId = userIds[i];
                var entries = await userEntries[i];

                if (entries.Length == 0)
                {
                    missedUserIds.Add(userId);
                    continue;
                }

                var user = ParseUserFromRedis(userId, entries);
                if (user is null)
                {
                    missedUserIds.Add(userId);
                    continue;
                }

                usersById[userId] = user;
            }

            if (missedUserIds.Count > 0)
            {
                var dbUsers = await context.Users
                    .Where(u => missedUserIds.Contains(u.UserId))
                    .ToListAsync(ct);

                foreach (var dbUser in dbUsers)
                {
                    usersById[dbUser.UserId] = dbUser;
                }

                await Task.WhenAll(dbUsers.Select(SetUserCacheAsync));
            }

            return userIds
                .Where(usersById.ContainsKey)
                .Select(userId => usersById[userId])
                .ToList();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get users by ids");
            throw;
        }
    }

    public async Task<User?> GetByPublicIdAsync(string publicId, CancellationToken ct = default)
    {
        try
        {
            var userId = await _database.StringGetAsync($"{UserPublicIdMappingKey}:{publicId}");
            if (userId.HasValue && long.TryParse(userId.ToString(), out var id))
                return await GetByIdAsync(id, ct);

            var user = await context.Users.SingleOrDefaultAsync(u => u.PublicId == publicId, ct);
            if (user is null)
                throw new KeyNotFoundException($"User not found for public id {publicId}");

            await SetUserCacheAsync(user);
            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user by public id {PublicId}", publicId);
            throw;
        }
    }

    private async Task SetUserCacheAsync(User user)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync($"{UserKey}:{user.UserId}", [
            new HashEntry("UserId", user.UserId),
            new HashEntry("PublicId", user.PublicId),
            new HashEntry("CreatedAt", user.CreatedAt.ToString("O"))
        ]);
        _ = transaction.KeyExpireAsync($"{UserKey}:{user.UserId}", RedisSettings.RedisCacheTtl);
        _ = transaction.StringSetAsync(
            $"{UserPublicIdMappingKey}:{user.PublicId}",
            user.UserId,
            RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set user cache");
    }

    private async Task DeleteUserCacheAsync(long userId, params string[] publicIds)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.KeyDeleteAsync($"{UserKey}:{userId}");
        foreach (var publicId in publicIds.Where(publicId => !string.IsNullOrWhiteSpace(publicId)).Distinct())
        {
            _ = transaction.KeyDeleteAsync($"{UserPublicIdMappingKey}:{publicId}");
        }

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete user cache");
    }

    private User? ParseUserFromRedis(long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("PublicId", out var publicId) ||
            !dict.TryGetValue("CreatedAt", out var createdAtStr))
        {
            logger.LogWarning("User {UserId} has missing fields", userId);
            return null;
        }

        if (!long.TryParse(userIdStr, out var id))
            return null;

        if (!DateTime.TryParse(createdAtStr, out var createdAt))
            createdAt = DateTime.UtcNow;

        return User.FromRedis(id, publicId, createdAt);
    }
}