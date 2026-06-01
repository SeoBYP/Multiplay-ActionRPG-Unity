using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

public class UserProfileRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<UserProfileRepository> logger) : IUserProfileRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<UserProfile> CreateAsync(long userId, string nickName, CancellationToken ct = default)
    {
        try
        {
            var newUserProfile = UserProfile.Create(userId, nickName);
            
            var userProfileEntry = await context.UserProfiles.AddAsync(newUserProfile, ct);
            await context.SaveChangesAsync(ct);

            var userProfile = userProfileEntry.Entity;
            await SetUserProfileAsync(userProfile);

            return userProfile;
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
            var entries = await _database.HashGetAllAsync(RedisKeys.UserProfile(userId));
            if (entries.Length > 0)
                return ParseUserProfileFromRedis(userId, entries);

            var userProfile = await context.UserProfiles.AsNoTracking().SingleOrDefaultAsync(up => up.UserId == userId, ct);
            if (userProfile == null)
                throw new KeyNotFoundException($"User profile not found for user id {userId}");

            await SetUserProfileAsync(userProfile);
            return userProfile;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user profile");
            throw;
        }
    }

    public async Task<UserProfile?> GetByNicknameAsync(string nickName, CancellationToken ct = default)
    {
        try
        {
            return await context.UserProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(up => up.NickName == nickName, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user profile by nickname");
            throw;
        }
    }

    public async Task<bool> UpdateAsync(UserProfile profile, CancellationToken ct = default)
    {
        try
        {
            if (profile.UserId <= 0)
                throw new InvalidOperationException("UpdateAsync requires an existing user profile");
            
            var existingProfile = await context.UserProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(up => up.UserId == profile.UserId, ct);
            if (existingProfile is null)
                throw new KeyNotFoundException($"User profile not found for user id {profile.UserId}");
            
            context.UserProfiles.Update(profile);
            await context.SaveChangesAsync(ct);
            
            await DeleteUserProfileAsync(profile.UserId);

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
            var profile = await context.UserProfiles.SingleOrDefaultAsync(up => up.UserId == userId, ct);
            if (profile is not null)
            {
                context.UserProfiles.Remove(profile);
                await context.SaveChangesAsync(ct);
            }
            
            await DeleteUserProfileAsync(userId);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove user profile");
            throw;
        }
    }
    
    private async Task SetUserProfileAsync(UserProfile profile)
    {
        var transaction = _database.CreateTransaction();
            
        _ = transaction.HashSetAsync(RedisKeys.UserProfile(profile.UserId), [
            new HashEntry("UserId", profile.UserId),
            new HashEntry("NickName", profile.NickName)
        ]);
            
        _ = transaction.KeyExpireAsync(RedisKeys.UserProfile(profile.UserId), RedisSettings.RedisCacheTtl);
        
        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to create user: transaction rolled back");
    }

    private async Task DeleteUserProfileAsync(long userId)
    {
        var transaction = _database.CreateTransaction();
        _ = transaction.KeyDeleteAsync(RedisKeys.UserProfile(userId));
        bool committed = await transaction.ExecuteAsync();
        
        if (!committed)
            throw new InvalidOperationException("Failed to delete user cache");
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
