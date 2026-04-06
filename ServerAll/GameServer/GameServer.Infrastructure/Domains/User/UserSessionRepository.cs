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

/// <summary>
/// Redis 기반 사용자 세션 저장소
/// </summary>
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

    /// <summary>
    /// 새로운 사용자 세션을 생성하고 Redis에 저장합니다.
    /// </summary>
    /// <param name="userId">사용자 고유 식별자</param>
    /// <returns>생성된 세션 객체, 실패 시 예외 발생</returns>
    public async Task<UserSession?> CreateSessionAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var sessionId = Guid.CreateVersion7().ToString();
            var newSession = UserSession.Create(userId, sessionId);

            // DB에 세션 저장
            var entry = await context.UserSessions.AddAsync(newSession, ct);
            await context.SaveChangesAsync(ct);

            // Redis 캐시 설정
            await SetSessionCacheAsync(entry.Entity);

            return entry.Entity;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create session for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 세션 ID를 사용하여 활성화된 세션 정보를 조회합니다.
    /// </summary>
    /// <param name="sessionId">조회할 세션 ID</param>
    /// <returns>세션 정보 객체, 없거나 만료된 경우 null</returns>
    public async Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync(RedisKeys.UserSession(sessionId));
            if (entries.Length > 0)
                return ParseSessionFromEntries(sessionId, entries);

            // Cache Miss: DB 조회
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

    /// <summary>
    /// 사용자 ID를 사용하여 해당 사용자의 현재 세션을 조회합니다.
    /// </summary>
    /// <param name="userId">조회할 사용자 ID</param>
    /// <returns>세션 정보 객체, 세션이 없는 경우 null</returns>
    public async Task<UserSession?> GetSessionByUserIdAsync(long userId, CancellationToken ct = default)
    {
        try
        {
            var sessionId = await _database.StringGetAsync(RedisKeys.UserSessionMapping(userId));
            if (sessionId.HasValue && !string.IsNullOrWhiteSpace(sessionId.ToString()))
                return await GetBySessionIdAsync(sessionId.ToString(), ct);

            // Cache Miss: DB 조회
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

    /// <summary>
    /// 지정된 세션 ID에 해당하는 세션 정보를 삭제합니다.
    /// </summary>
    /// <param name="sessionId">삭제할 세션 ID</param>
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
                // DB에 없더라도 캐시 정리를 시도한다 (안전망)
                // 세션 키에서 UserId를 가져와야 매핑 키를 지울 수 있음
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

        // Redis Hash에 세션 정보 저장
        _ = transaction.HashSetAsync(RedisKeys.UserSession(session.SessionId),
        [
            new HashEntry("UserId", session.UserId),
            new HashEntry("LoginAt", session.LoginAt.ToString("O")),
            new HashEntry("LastActiveAt", session.LastActiveAt.ToString("O"))
        ]);

        // 세션 TTL(Time To Live) 설정
        _ = transaction.KeyExpireAsync(RedisKeys.UserSession(session.SessionId), ttl);

        // 활성 세션 Set에 sessionId 추가 (현재 접속 세션 추적)
        _ = transaction.SortedSetAddAsync(
            RedisKeys.UserSessionActive(),
            session.SessionId,
            DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds()  // score = 만료 시각
        );

        // UserId → SessionId 매핑 저장 (사용자당 하나의 세션 관리)
        _ = transaction.StringSetAsync(
            RedisKeys.UserSessionMapping(session.UserId),
            session.SessionId,
            ttl
        );

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to set session cache");
    }

    private async Task DeleteSessionCacheAsync(string sessionId, long? userId)
    {
        var transaction = _database.CreateTransaction();

        // 세션 데이터 삭제
        _ = transaction.KeyDeleteAsync(RedisKeys.UserSession(sessionId));

        // 활성 세션 목록에서 제거
        _ = transaction.SortedSetRemoveAsync(RedisKeys.UserSessionActive(), sessionId);
        
        // UserId → SessionId 매핑 삭제
        if (userId.HasValue)
            _ = transaction.KeyDeleteAsync(RedisKeys.UserSessionMapping(userId.Value));

        bool committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Failed to delete session cache");
    }

    /// <summary>
    /// 현재 시스템에서 활성화된 전체 세션의 개수를 반환합니다.
    /// </summary>
    public async Task<long> GetActiveSessionCountAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return await _database.SortedSetLengthAsync(RedisKeys.UserSessionActive(), now, double.PositiveInfinity);

    }

    /// <summary>
    /// 현재 활성화된 모든 세션 목록을 조회합니다.
    /// </summary>
    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var sessionIdValues = await _database.SortedSetRangeByScoreAsync(
                RedisKeys.UserSessionActive(), now, double.PositiveInfinity);

            if (sessionIdValues.Length == 0)
            {
                // Redis가 비어있는 경우 DB에서 활성 세션을 가져옴
                return await context.UserSessions.ToListAsync(ct);
            }

            var sessions = new List<UserSession>();
            var missedSessionIds = new List<string>();

            // Batch를 사용하여 각 세션의 상세 정보(Hash)를 한꺼번에 조회
            var batch = _database.CreateBatch();
            var sessionIds = sessionIdValues.Select(v => v.ToString()).ToList();
            var hashTasks = sessionIds
                .Select(id => batch.HashGetAllAsync(RedisKeys.UserSession(id)))
                .ToList();

            batch.Execute();

            for (int i = 0; i < sessionIds.Count; i++)
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
                
                // DB에서 찾은 세션들 캐시 복구 (비동기로 실행하되 await 함)
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

    /// <summary>
    /// Redis 키 만료(TTL)로 인해 사라진 세션들을 활성 세션 목록(Set)에서 정리합니다.
    /// </summary>
    /// <param name="timeout">정리 작업 시 고려할 타임아웃 설정 (현재 로직에서는 존재 여부 확인용)</param>
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
    
    /// <summary>
    /// Redis에서 가져온 Hash 항목들을 UserSession 객체로 변환합니다.
    /// </summary>
    private UserSession? ParseSessionFromEntries(string sessionId, HashEntry[] entries)
    {
        // HashEntry 배열을 Dictionary로 변환
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        // 필수 필드 검증
        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !long.TryParse(userIdStr, out var userId))
        {
            logger.LogWarning("Session {SessionId} has missing or invalid UserId", sessionId);
            return null;
        }

        // 날짜 파싱 (ISO 8601)
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
