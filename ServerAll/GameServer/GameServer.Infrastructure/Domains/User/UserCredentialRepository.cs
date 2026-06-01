using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

public class UserCredentialRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    ILogger<UserCredentialRepository> logger) : IUserCredentialRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    public async Task<UserCredential> CreateAsync(long userId, string email, string passwordHash,
        CancellationToken ct = default)
    {
        try
        {
            var newUserCredential = UserCredential.Create(userId, email, passwordHash);

            var userCredentialEntry = await context.UserCredentials.AddAsync(newUserCredential, ct);
            await context.SaveChangesAsync(ct);

            var credential = userCredentialEntry.Entity;
            await SetUserCredentialAsync(credential);

            return credential;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create user credential for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserCredential?> FindByIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserCredential(userId));
            if (entries.Length > 0)
                return ParseUserCredentialFromRedis(userId, entries);

            var userCredential = await context.UserCredentials.AsNoTracking().SingleOrDefaultAsync(uc => uc.UserId == userId, ct);
            if (userCredential is null)
                throw new KeyNotFoundException($"User credential not found for user id {userId}");

            await SetUserCredentialAsync(userCredential);
            return userCredential;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user credential by user id {UserId}", userId);
            throw;
        }
    }

    public async Task<UserCredential?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        try
        {
            var userId = await _database.StringGetAsync(RedisKeys.UserCredentialEmailMapping(email));
            if (userId.HasValue && long.TryParse(userId.ToString(), out var id))
                return await FindByIdAsync(id, ct);

            var userCredential = await context.UserCredentials.AsNoTracking().SingleOrDefaultAsync(uc => uc.Email == email, ct);
            if (userCredential is null)
                return null;

            await SetUserCredentialAsync(userCredential);
            return userCredential;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user credential by email {Email}", email);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(UserCredential userCredential, CancellationToken ct = default)
    {
        try
        {
            if (userCredential.UserId <= 0)
                throw new InvalidOperationException("Invalid user id");

            var existingCredential = await context.UserCredentials
                .AsNoTracking()
                .SingleOrDefaultAsync(uc => uc.UserId == userCredential.UserId, ct);
            if (existingCredential is null)
                throw new KeyNotFoundException($"User credential not found for user id {userCredential.UserId}");

            var trackedCredential = context.ChangeTracker.Entries<UserCredential>()
                .FirstOrDefault(entry => entry.Entity.UserId == userCredential.UserId);

            if (trackedCredential is not null)
            {
                trackedCredential.CurrentValues.SetValues(userCredential);
            }
            else
            {
                context.UserCredentials.Update(userCredential);
            }

            await context.SaveChangesAsync(ct);

            // Email이 변경된 경우 이전/신규 매핑 키를 모두 비워 다음 조회에서 DB 기준으로 재구성한다.
            await DeleteUserCredentialCacheAsync(userCredential.UserId, existingCredential.Email, userCredential.Email);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update user credential for user {UserId}", userCredential.UserId);
            throw;
        }
    }

    public async Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct = default)
    {
        try
        {
            var credential = await context.UserCredentials.SingleOrDefaultAsync(uc => uc.UserId == userId, ct);
            if (credential is null)
                throw new KeyNotFoundException($"User credential not found for user id {userId}");

            credential.UpdatePasswordHash(passwordHash);
            await context.SaveChangesAsync(ct);
            await DeleteUserCredentialCacheAsync(userId, credential.Email);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update password hash for user {UserId}", userId);
            throw;
        }
    }
    
    public async Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var credential = await context.UserCredentials.SingleOrDefaultAsync(uc => uc.UserId == userId, ct);
            if (credential is null)
                throw new KeyNotFoundException($"User credential not found for user id {userId}");

            credential.ClearRefreshToken();

            await context.SaveChangesAsync(ct);
            await DeleteUserCredentialCacheAsync(userId, credential.Email);
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to clear refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var credential = await context.UserCredentials.SingleOrDefaultAsync(uc => uc.UserId == userId, ct);
            if (credential is not null)
            {
                context.UserCredentials.Remove(credential);
                await context.SaveChangesAsync(ct);
            }

            if (credential is null)
            {
                await DeleteUserCredentialCacheAsync(userId);
            }
            else
            {
                await DeleteUserCredentialCacheAsync(userId, credential.Email);
            }

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove user credential for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default)
    {
        var credential = await FindByEmailAsync(email, ct);
        return credential is not null;
    }

    private async Task SetUserCredentialAsync(UserCredential credential)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.UserCredential(credential.UserId), [
            new HashEntry("UserId", credential.UserId),
            new HashEntry("Email", credential.Email),
            new HashEntry("PasswordHash", credential.PasswordHash),
            new HashEntry("RefreshToken", credential.RefreshToken ?? string.Empty),
            new HashEntry("RefreshTokenVersion", credential.RefreshTokenVersion),
            new HashEntry("RefreshTokenExpiresAt", credential.RefreshTokenExpiresAt?.ToString("O") ?? string.Empty)
        ]);
        _ = transaction.KeyExpireAsync(RedisKeys.UserCredential(credential.UserId), RedisSettings.RedisCacheTtl);

        _ = transaction.StringSetAsync(
            RedisKeys.UserCredentialEmailMapping(credential.Email),
            credential.UserId,
            RedisSettings.RedisCacheTtl);

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set user credential cache");
    }

    private async Task DeleteUserCredentialCacheAsync(long userId, params string[] emails)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.KeyDeleteAsync(RedisKeys.UserCredential(userId));
        foreach (var email in emails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct())
        {
            _ = transaction.KeyDeleteAsync(RedisKeys.UserCredentialEmailMapping(email));
        }

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete user credential cache");
    }

    private UserCredential? ParseUserCredentialFromRedis(long userId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("Email", out var email) ||
            !dict.TryGetValue("PasswordHash", out var passwordHash))
        {
            logger.LogWarning("User credential {UserId} has missing fields", userId);
            return null;
        }

        if (!long.TryParse(userIdStr, out var parsedUserId))
            return null;

        dict.TryGetValue("RefreshToken", out var refreshToken);
        var normalizedToken = string.IsNullOrEmpty(refreshToken) ? null : refreshToken;

        int refreshTokenVersion = 0;
        if (dict.TryGetValue("RefreshTokenVersion", out var versionStr) &&
            int.TryParse(versionStr, out var parsedVersion))
        {
            refreshTokenVersion = parsedVersion;
        }

        DateTime? refreshTokenExpiresAt = null;
        if (dict.TryGetValue("RefreshTokenExpiresAt", out var expiresAtStr) &&
            DateTime.TryParse(expiresAtStr, out var parsedExpiry))
        {
            refreshTokenExpiresAt = parsedExpiry;
        }

        return UserCredential.Restore(parsedUserId, email, passwordHash, normalizedToken, refreshTokenVersion, refreshTokenExpiresAt);
    }
}
