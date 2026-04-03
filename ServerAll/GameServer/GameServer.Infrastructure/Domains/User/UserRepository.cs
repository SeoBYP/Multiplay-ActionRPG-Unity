using GameServer.Application.Domains.User.Interfaces;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

using User = Domain.Entities.User.User;

/// <summary>
/// 데이터베이스 연동 사용자 저장소 구현체 (미구현)
/// </summary>
public class UserRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<UserRepository> logger) : IUserRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string UserKey = "game:user";
    private const string UserCounterKey = "game:user:id:counter";
    private const string UserPublicIdMappingKey = "game:user:publicid";

    /// <summary>
    /// 새로운 사용자를 추가합니다. (회원가입)
    /// </summary>
    public async Task<User> CreateAsync(CancellationToken ct = default)
    {
        try
        {
            var newUser = User.Create();
            
            var userEntity = context.Users.AddAsync(newUser, ct);
            await context.SaveChangesAsync(ct);

            var user = userEntity.Result.Entity;
            
            var transaction = _database.CreateTransaction();
            
            // 유저 정보 저장(Redis Cache)
            _ = transaction.HashSetAsync($"{UserKey}:{user.UserId}", [
                new HashEntry("UserId", user.UserId),
                new HashEntry("PublicId", user.PublicId),
                new HashEntry("CreatedAt", user.CreatedAt.ToString("O"))
            ]);
            _ = transaction.StringSetAsync($"{UserPublicIdMappingKey}:{user.PublicId}", user.UserId,
                when: When.NotExists);
                
            // 4. 트랜잭션 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create user: transaction rolled back");

            return user;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add user");
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var user = await GetByIdAsync(userId, ct);
            if (user is null)
                return true;
            
            var transaction = _database.CreateTransaction();

            var tasks = new List<Task>();
            tasks.Add(transaction.KeyDeleteAsync($"{UserKey}:{userId}"));
            tasks.Add(transaction.KeyDeleteAsync($"{UserPublicIdMappingKey}:{user.PublicId}"));


            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to remove user: transaction rolled back");

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

            // 기존 User 정보 가져오기
            var existingUser = await GetByIdAsync(user.UserId, ct);
            if (existingUser is null)
                return false;

            var transaction = _database.CreateTransaction();

            // 1. 기존 정보 삭제 및 새 정보 업데이트 (트랜잭션에 포함)
            var tasks = new List<Task>();
            if (existingUser.PublicId != user.PublicId)
            {
                tasks.Add(transaction.KeyDeleteAsync($"{UserPublicIdMappingKey}:{existingUser.PublicId}"));
                tasks.Add(transaction.StringSetAsync($"{UserPublicIdMappingKey}:{user.PublicId}", user.UserId));
            }

            // 2. 유저 본체 정보 업데이트
            tasks.Add(transaction.HashSetAsync($"{UserKey}:{user.UserId}", [
                new HashEntry("UserId", user.UserId),
                new HashEntry("PublicId", user.PublicId),
                new HashEntry("CreatedAt", user.CreatedAt.ToString("O"))
            ]));

            // 3. 트랜잭션 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to update user: transaction rolled back");
            
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update user {UserId}", user.UserId);
            throw;
        }
    }

    public async Task<User?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            // DB 데이터 조회
            var user = await context.Users.
                SingleAsync(u => u.UserId == userId, ct);
            return user;
            
            var entries = await _database.HashGetAllAsync($"{UserKey}:{userId}");
            if (entries.Length == 0)
            {
                return null;
            }
            
            return ParseUserFromRedis(userId, entries);
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
            var batch = _database.CreateBatch();
            var userEntries = userIds
                .Select(userId => batch.HashGetAllAsync($"{UserKey}:{userId}"))
                .ToList();
            
            batch.Execute();
            
            var users = new List<User>();
            // idList.Count는 O(1) 속성 접근 (List<T>의 경우)
            for (int i = 0; i < userIds.Count; i++)
            {
                var entries = await userEntries[i];
                if (entries.Length == 0)
                    continue;
                
                // idList[i]는 O(1) 인덱서 접근 (List<T>의 경우)
                var user = ParseUserFromRedis(userIds[i], entries);
                if (user is not null)
                    users.Add(user);
            }

            return users;
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
            if (!userId.HasValue)
                return null;
            if (long.TryParse(userId.ToString(), out var id))
            {
                return await GetByIdAsync(id, ct);
            }

            return null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user by public id {PublicId}", publicId);
            throw;
        }
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
        {
            return null;
        }

        if (!DateTime.TryParse(createdAtStr, out var createdAt))
        {
            createdAt = DateTime.UtcNow;
        }

        // 리프레시 토큰 정보 추출 (선택적)
        string? refreshToken = dict.GetValueOrDefault("RefreshToken");
        DateTime refreshTokenExpiresAt = default;
        if (dict.TryGetValue("RefreshTokenExpiresAt", out var expiresAtStr))
        {
            DateTime.TryParse(expiresAtStr, out refreshTokenExpiresAt);
        }

        return User.FromRedis(id, publicId, createdAt);
    }
    
    
}
