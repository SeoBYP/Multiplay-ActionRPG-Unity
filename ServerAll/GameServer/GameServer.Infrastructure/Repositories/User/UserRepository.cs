using GameServer.Infrastructure.Interfaces.User;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories.User;

using User = Domain.Entities.User.User;

/// <summary>
/// 데이터베이스 연동 사용자 저장소 구현체 (미구현)
/// </summary>
public class UserRepository(IConnectionMultiplexer connectionMultiplexer) : IUserRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string UserKey = "game:user";
    private const string UserCounterKey = "game:user:id:counter";
    private const string UserEmailMappingKey = "game:user:email";
    private const string UserNicknameMappingKey = "game:user:nickname";
    private const string UserPublicIdMappingKey = "game:user:publicid";

    /// <summary>
    /// 새로운 사용자를 추가합니다. (회원가입)
    /// </summary>
    public async Task<User> AddAsync(string passwordHash, string email, CancellationToken ct = default)
    {
        try
        {
            var user = User.Create(passwordHash, email);

            var userId = await _database.StringIncrementAsync(UserCounterKey);
            user.SetUserId(userId);

            var transaction = _database.CreateTransaction();

            // 유저 정보 저장
            var tasks = new List<Task>();
            tasks.Add(transaction.HashSetAsync($"{UserKey}:{user.UserId}", [
                new HashEntry("UserId", user.UserId),
                new HashEntry("NickName", user.NickName),
                new HashEntry("Email", user.Email),
                new HashEntry("PublicId", user.PublicId),
                new HashEntry("PasswordHash", user.PasswordHash),
                new HashEntry("CreatedAt", user.CreatedAt.ToString("O"))
            ]));
            tasks.Add(transaction.StringSetAsync($"{UserEmailMappingKey}:{user.Email}", user.UserId, when: When.NotExists));
            tasks.Add(transaction.StringSetAsync($"{UserNicknameMappingKey}:{user.NickName}", user.UserId, when: When.NotExists));
            tasks.Add(transaction.StringSetAsync($"{UserPublicIdMappingKey}:{user.PublicId}", user.UserId, when: When.NotExists));
            

            // 4. 트랜잭션 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create user: transaction rolled back");

            await Task.WhenAll(tasks);
            return user;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
            tasks.Add(transaction.KeyDeleteAsync($"{UserNicknameMappingKey}:{user.NickName}"));
            tasks.Add(transaction.KeyDeleteAsync($"{UserEmailMappingKey}:{user.Email}"));
            tasks.Add(transaction.KeyDeleteAsync($"{UserPublicIdMappingKey}:{user.PublicId}"));


            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to remove user: transaction rolled back");

            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
            if (existingUser.NickName != user.NickName)
            {
                tasks.Add(transaction.KeyDeleteAsync($"{UserNicknameMappingKey}:{existingUser.NickName}"));
                tasks.Add(transaction.StringSetAsync($"{UserNicknameMappingKey}:{user.NickName}", user.UserId));
            }

            if (existingUser.Email != user.Email)
            {
                tasks.Add(transaction.KeyDeleteAsync($"{UserEmailMappingKey}:{existingUser.Email}"));
                tasks.Add(transaction.StringSetAsync($"{UserEmailMappingKey}:{user.Email}", user.UserId));
            }

            if (existingUser.PublicId != user.PublicId)
            {
                tasks.Add(transaction.KeyDeleteAsync($"{UserPublicIdMappingKey}:{existingUser.PublicId}"));
                tasks.Add(transaction.StringSetAsync($"{UserPublicIdMappingKey}:{user.PublicId}", user.UserId));
            }

            // 2. 유저 본체 정보 업데이트
            tasks.Add(transaction.HashSetAsync($"{UserKey}:{user.UserId}", [
                new HashEntry("UserId", user.UserId),
                new HashEntry("NickName", user.NickName),
                new HashEntry("Email", user.Email),
                new HashEntry("PublicId", user.PublicId),
                new HashEntry("PasswordHash", user.PasswordHash),
                new HashEntry("CreatedAt", user.CreatedAt.ToString("O"))
            ]));

            // 3. 트랜잭션 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to update user: transaction rolled back");
            
            await Task.WhenAll(tasks);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<User?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{UserKey}:{userId}");
            if (entries.Length == 0)
                return null;
            return ParseUserFromRedis(userId, entries);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var userId = await _database.StringGetAsync($"{UserEmailMappingKey}:{email}");
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
            Console.WriteLine(e);
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
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<User?> GetByNicknameAsync(string nickname, CancellationToken ct = default)
    {
        try
        {
            var userId = await _database.StringGetAsync($"{UserNicknameMappingKey}:{nickname}");
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
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default)
    {
        var userId = await _database.StringGetAsync($"{UserEmailMappingKey}:{email}");
        return userId.HasValue;
    }

    public async Task<bool> IsNicknameExistsAsync(string nickname, CancellationToken ct = default)
    {
        var userId = await _database.StringGetAsync($"{UserNicknameMappingKey}:{nickname}");
        return userId.HasValue;
    }

    private User? ParseUserFromRedis(long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("NickName", out var nickName) ||
            !dict.TryGetValue("Email", out var email) ||
            !dict.TryGetValue("PublicId", out var publicId) ||
            !dict.TryGetValue("PasswordHash", out var passwordHash) ||
            !dict.TryGetValue("CreatedAt", out var createdAtStr))
        {
            Console.WriteLine($"User {userId} has missing fields");
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

        return User.FromRedis(id, email, publicId, passwordHash, createdAt, nickName);
    }
}