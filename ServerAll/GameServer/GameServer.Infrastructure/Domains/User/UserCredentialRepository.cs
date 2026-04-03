using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities.User;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

public class UserCredentialRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<UserCredentialRepository> logger) : IUserCredentialRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string UserCredentialKey = "game:user:credential:id";
    private const string UserCredentialEmailMappingKey = "game:user:credential:email";

    public async Task<UserCredential> CreateAsync(long userId, string email, string passwordHash,
        CancellationToken ct = default)
    {
        try
        {
            var credential = UserCredential.Create(userId, email, passwordHash);
            var transaction = _database.CreateTransaction();

            _ = transaction.HashSetAsync($"{UserCredentialKey}:{userId}", [
                new HashEntry("UserId", credential.UserId),
                new HashEntry("Email", credential.Email),
                new HashEntry("PasswordHash", credential.PasswordHash)
            ]);
            _ = transaction.StringSetAsync($"{UserCredentialEmailMappingKey}:{credential.Email}", credential.UserId,
                when: When.NotExists);

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create user credential: transaction rolled back");

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
            var entries = await _database.HashGetAllAsync($"{UserCredentialKey}:{userId}");
            if (entries.Length == 0)
                return null;

            return ParseUserCredentialFromRedis(userId, entries);
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
            var userId = await _database.StringGetAsync($"{UserCredentialEmailMappingKey}:{email}");
            if (!userId.HasValue)
                return null;

            return long.TryParse(userId.ToString(), out var id)
                ? await FindByIdAsync(id, ct)
                : null;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get user credential by email {Email}", email);
            throw;
        }
    }

    public async Task<bool> UpdatePasswordHashAsync(long userId, string passwordHash, CancellationToken ct = default)
    {
        try
        {
            var key = $"{UserCredentialKey}:{userId}";
            var transaction = _database.CreateTransaction();
            transaction.AddCondition(Condition.KeyExists(key));

            _ = transaction.HashSetAsync(key, [new HashEntry("PasswordHash", passwordHash)]);
            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update password hash for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> UpdateRefreshTokenAsync(long userId, string hashedToken, DateTime expiry, CancellationToken ct = default)
    {
        try
        {
            var key = $"{UserCredentialKey}:{userId}";
            var transaction = _database.CreateTransaction();
            transaction.AddCondition(Condition.KeyExists(key));

            _ = transaction.HashSetAsync(key, [
                new HashEntry("RefreshToken", hashedToken),
                new HashEntry("RefreshTokenExpiresAt", expiry.ToString("O"))
            ]);

            return await transaction.ExecuteAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ClearRefreshTokenAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            return await _database.HashDeleteAsync($"{UserCredentialKey}:{userId}", ["RefreshToken", "RefreshTokenExpiresAt"]) > 0;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to clear refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(UserCredential userCredential)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            _ = transaction.KeyDeleteAsync($"{UserCredentialKey}:{userCredential.UserId}");
            _ = transaction.KeyDeleteAsync($"{UserCredentialEmailMappingKey}:{userCredential.Email}");

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to remove user credential: transaction rolled back");

            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove user credential for user {UserId}", userCredential.UserId);
            throw;
        }
    }

    public async Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default)
    {
        try
        {
            return await _database.KeyExistsAsync($"{UserCredentialEmailMappingKey}:{email}");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to check email existence for {Email}", email);
            throw;
        }
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

        var refreshTokenExpiresAt = default(DateTime);
        if (dict.TryGetValue("RefreshTokenExpiresAt", out var expiresAtStr))
            DateTime.TryParse(expiresAtStr, out refreshTokenExpiresAt);

        return UserCredential.Restore(parsedUserId, email, passwordHash, refreshToken, refreshTokenExpiresAt);
    }
}
