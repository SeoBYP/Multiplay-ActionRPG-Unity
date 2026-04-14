using System.Globalization;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Application.Security;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.User;

public class UserSessionRepository(
    IConnectionMultiplexer connectionMultiplexer,
    GameServerDbContext context,
    IOptions<JwtOptions> jwtOptions,
    ILogger<UserSessionRepository> logger)
    : IUserSessionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    private TimeSpan SessionTtl => _jwtOptions.AccessTokenExpiration;

    public async Task<UserSession?> CreateSessionAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var existingSession = await context.UserSessions.SingleOrDefaultAsync(us => us.UserId == userId, ct);
            if (existingSession is not null)
            {
                context.UserSessions.Remove(existingSession);
                await context.SaveChangesAsync(ct);
                await DeleteSessionCacheAsync(existingSession.SessionId, existingSession.UserId);
            }

            var sessionId = Guid.CreateVersion7().ToString();
            var newSession = UserSession.Create(userId, sessionId);

            var entry = await context.UserSessions.AddAsync(newSession, ct);
            await context.SaveChangesAsync(ct);

            await SetSessionCacheAsync(entry.Entity);
            return entry.Entity;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create session for user {UserId}", userId);
            throw;
        }
    }

    public async Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserSession(sessionId));
            if (entries.Length > 0)
                return ParseSessionFromEntries(sessionId, entries);

            var session = await context.UserSessions.SingleOrDefaultAsync(us => us.SessionId == sessionId, ct);
            if (session is null)
                return null;

            await SetSessionCacheAsync(session);
            return session;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error while getting session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<UserSession?> GetSessionByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var sessionId = await _database.StringGetAsync(RedisKeys.UserSessionMapping(userId));
            if (sessionId.HasValue && !string.IsNullOrWhiteSpace(sessionId.ToString()))
                return await GetBySessionIdAsync(sessionId.ToString(), ct);

            var session = await context.UserSessions.SingleOrDefaultAsync(us => us.UserId == userId, ct);
            if (session is null)
                return null;

            await SetSessionCacheAsync(session);
            return session;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unexpected error while getting session by user id {UserId}", userId);
            throw;
        }
    }

    public async Task RemoveSessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await context.UserSessions.SingleOrDefaultAsync(us => us.SessionId == sessionId, ct);
            if (session is not null)
            {
                context.UserSessions.Remove(session);
                await context.SaveChangesAsync(ct);
            }

            if (session is null)
            {
                var userIdValue = await _database.HashGetAsync(RedisKeys.UserSession(sessionId), "UserId");
                if (userIdValue.HasValue && long.TryParse(userIdValue.ToString(), out var userId))
                {
                    await DeleteSessionCacheAsync(sessionId, userId);
                }
                else
                {
                    await DeleteSessionCacheAsync(sessionId, null);
                }
            }
            else
            {
                await DeleteSessionCacheAsync(session.SessionId, session.UserId);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to remove session {SessionId}", sessionId);
            throw;
        }
    }

    private async Task SetSessionCacheAsync(UserSession session)
    {
        var ttl = SessionTtl;
        var transaction = _database.CreateTransaction();

        _ = transaction.HashSetAsync(RedisKeys.UserSession(session.SessionId),
        [
            new HashEntry("UserId", session.UserId),
            new HashEntry("LoginAt", session.LoginAt.ToString("O")),
            new HashEntry("LastActiveAt", session.LastActiveAt.ToString("O"))
        ]);

        _ = transaction.KeyExpireAsync(RedisKeys.UserSession(session.SessionId), ttl);

        _ = transaction.SortedSetAddAsync(
            RedisKeys.UserSessionActive(),
            session.SessionId,
            DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds());

        _ = transaction.StringSetAsync(
            RedisKeys.UserSessionMapping(session.UserId),
            session.SessionId,
            ttl);

        var committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set session cache");
    }

    private async Task DeleteSessionCacheAsync(string sessionId, long? userId)
    {
        var transaction = _database.CreateTransaction();

        _ = transaction.KeyDeleteAsync(RedisKeys.UserSession(sessionId));
        _ = transaction.SortedSetRemoveAsync(RedisKeys.UserSessionActive(), sessionId);

        if (userId.HasValue)
            _ = transaction.KeyDeleteAsync(RedisKeys.UserSessionMapping(userId.Value));

        var committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete session cache");
    }

    public async Task<long> GetActiveSessionCountAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await _database.SortedSetLengthAsync(RedisKeys.UserSessionActive(), now, double.PositiveInfinity);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sessionIdValues = await _database.SortedSetRangeByScoreAsync(
                RedisKeys.UserSessionActive(), now, double.PositiveInfinity);

            if (sessionIdValues.Length == 0)
                return await context.UserSessions.ToListAsync(ct);

            var sessions = new List<UserSession>();
            var missedSessionIds = new List<string>();

            var batch = _database.CreateBatch();
            var sessionIds = sessionIdValues.Select(v => v.ToString()).ToList();
            var hashTasks = sessionIds
                .Select(id => batch.HashGetAllAsync(RedisKeys.UserSession(id)))
                .ToList();

            batch.Execute();

            for (var i = 0; i < sessionIds.Count; i++)
            {
                var sessionId = sessionIds[i];
                var entries = await hashTasks[i];

                if (entries.Length == 0)
                {
                    missedSessionIds.Add(sessionId);
                    continue;
                }

                var session = ParseSessionFromEntries(sessionId, entries);
                if (session is not null)
                {
                    sessions.Add(session);
                }
                else
                {
                    missedSessionIds.Add(sessionId);
                }
            }

            if (missedSessionIds.Count > 0)
            {
                var dbSessions = await context.UserSessions
                    .Where(us => missedSessionIds.Contains(us.SessionId))
                    .ToListAsync(ct);

                sessions.AddRange(dbSessions);
                await Task.WhenAll(dbSessions.Select(SetSessionCacheAsync));
            }

            return sessions;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get active sessions");
            throw;
        }
    }

    public async Task CleanupExpiredSessionsAsync(TimeSpan timeout)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var removed = await _database.SortedSetRemoveRangeByScoreAsync(RedisKeys.UserSessionActive(), 0, now);
            if (removed > 0)
                logger.LogInformation("Removed {ExpiredCount} expired sessions from active set", removed);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to cleanup expired sessions");
            throw;
        }
    }

    private UserSession? ParseSessionFromEntries(string sessionId, HashEntry[] entries)
    {
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !long.TryParse(userIdStr, out var userId))
        {
            logger.LogWarning("Session {SessionId} has missing or invalid UserId", sessionId);
            return null;
        }

        if (!dict.TryGetValue("LoginAt", out var loginAtStr) ||
            !DateTime.TryParse(loginAtStr, null, DateTimeStyles.RoundtripKind, out var loginAt))
        {
            loginAt = DateTime.UtcNow;
        }

        if (!dict.TryGetValue("LastActiveAt", out var lastActiveAtStr) ||
            !DateTime.TryParse(lastActiveAtStr, null, DateTimeStyles.RoundtripKind, out var lastActiveAt))
        {
            lastActiveAt = DateTime.UtcNow;
        }

        return UserSession.Restore(sessionId, userId, loginAt, lastActiveAt);
    }
}
