using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

public class UserProfileRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<UserProfileRepository> logger) : IUserProfileRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string UserProfileKey = "game:user:profile:id";

    public async Task<UserProfile> CreateAsync(long userId, string nickName, CancellationToken ct = default)
    {
        try
        {
            var profile = UserProfile.Create(userId, nickName);

            var transaction = _database.CreateTransaction();
            
            _ = transaction.HashSetAsync($"{UserProfileKey}:{profile.UserId}", [
                new HashEntry("UserId", profile.UserId),
                new HashEntry("NickName", profile.NickName)
            ]);
            
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create user: transaction rolled back");

            return profile;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add user");
            throw;
        }
    }

    public async Task<UserProfile?> GetByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{UserProfileKey}:{userId}");
            if (entries.Length == 0)
            {
                return null;
            }

            return ParseUserProfileFromRedis(userId, entries);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user profile");
            throw;
        }
    }

    public async Task<bool> UpdateAsync(UserProfile profile, CancellationToken ct = default)
    {
        try
        {
            var transaction = _database.CreateTransaction();
            
            _ = transaction.HashSetAsync($"{UserProfileKey}:{profile.UserId}", [
                new HashEntry("UserId", profile.UserId),
                new HashEntry("NickName", profile.NickName)
            ]);
            
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create user: transaction rolled back");
            
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user profile");
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var userProfile = await GetByIdAsync(userId, ct);
            if (userProfile is null)
                return false;
            
            var transaction = _database.CreateTransaction();

            await transaction.KeyDeleteAsync($"{UserProfileKey}:{userId}");
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove user profile");
            throw;
        }
    }
    
    private UserProfile? ParseUserProfileFromRedis(long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());
        
        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("NickName", out var nickName))
        {
            logger.LogWarning("User {UserId} has missing fields", userId);
            return null;
        }
        
        if (!long.TryParse(userIdStr, out var id))
        {
            return null;
        }
        
        var profile = UserProfile.FromRedis(userId, nickName);
        return profile;
    }
}