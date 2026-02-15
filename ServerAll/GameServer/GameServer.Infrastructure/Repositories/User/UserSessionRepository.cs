using System.Globalization;
using GameServer.Domain.Entities;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Infrastructure.Security;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories.User;

/// <summary>
/// Redis 기반 사용자 세션 저장소
/// </summary>
public class UserSessionRepository(IConnectionMultiplexer connectionMultiplexer, 
    IOptions<JwtOptions> jwtOptions)
    : IUserSessionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    
    private const string SessionKey = "game:session";
    private const string ActiveSessionsKey = "game:session:active";
    private const string UserSessionMappingKey = "game:user:session";
    
    private TimeSpan SessionTtl => _jwtOptions.AccessTokenExpiration;

    /// <summary>
    /// 사용자 세션 생성
    /// </summary>
    public async Task<UserSession?> CreateSessionAsync(long userId, string userName)
    {
        try
        {
            var ttl = SessionTtl;

            var transaction = _database.CreateTransaction();

            var sessionId = Guid.CreateVersion7().ToString();
            var newSession = UserSession.Create(userId, userName, sessionId);

            // Redis Hash에 세션 정보 저장
            Task hashTask = transaction.HashSetAsync($"{SessionKey}:{sessionId}",
            [
                new HashEntry("UserId", userId),
                new HashEntry("UserName", userName),
                new HashEntry("LoginAt", newSession.LoginAt.ToString("O")),
                new HashEntry("LastActiveAt", newSession.LastActiveAt.ToString("O"))
            ]);

            // 세션 TTL(Time To Live) 설정
            Task expireTask = transaction.KeyExpireAsync($"{SessionKey}:{sessionId}", ttl);

            // 활성 세션 Set에 sessionId 추가 (현재 접속 세션 추적)
            Task activeTask =  transaction.SetAddAsync(ActiveSessionsKey, sessionId);

            // UserId → SessionId 매핑 저장 (사용자당 하나의 세션 관리)
            Task mappingTask =  transaction.StringSetAsync(
                $"{UserSessionMappingKey}:{userId}",
                sessionId,
                ttl
            );

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
            {
                throw new InvalidOperationException("Failed to create session");
            }
            await Task.WhenAll(hashTask, expireTask, activeTask, mappingTask);
            return newSession;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to create session");
        }
    }

    /// <summary>
    /// SessionId로 세션 조회
    /// </summary>
    public async Task<UserSession?> GetBySessionIdAsync(string sessionId)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{SessionKey}:{sessionId}");
            if (entries.Length == 0)
                return null;

            return ParseSessionFromEntries(sessionId, entries);
        }
        catch (RedisException)
        {
            // Redis 통신 오류는 로그만 남기고 null 반환
            Console.WriteLine($"Redis error while getting session {sessionId}");
            return null;
        }
        catch
        {
            // 예상치 못한 오류는 상위로 전파
            Console.WriteLine($"Unexpected error while getting session {sessionId}");
            throw;
        }
    }

    /// <summary>
    /// UserId로 세션 조회
    /// </summary>
    public async Task<UserSession?> GetSessionByUserIdAsync(long userId)
    {
        var sessionId = await _database.StringGetAsync($"{UserSessionMappingKey}:{userId}");

        // 매핑 정보 없음
        if (!sessionId.HasValue)
            return null;

        // 값이 비어 있는 경우
        if (string.IsNullOrWhiteSpace(sessionId.ToString()))
            return null;

        return await GetBySessionIdAsync(sessionId.ToString());
    }

    /// <summary>
    /// 세션 제거
    /// </summary>
    public async Task RemoveSessionAsync(string sessionId)
    {
        try
        {
            // 세션에서 UserId 조회
            var userIdValue = await _database.HashGetAsync($"{SessionKey}:{sessionId}", "UserId");
            
            var transaction = _database.CreateTransaction();
            
            // 세션 데이터 삭제
            Task delSessionTask = transaction.KeyDeleteAsync($"{SessionKey}:{sessionId}");

            // 활성 세션 목록에서 제거
            Task removeActiveTask = transaction.SetRemoveAsync(ActiveSessionsKey, sessionId);

            // UserId → SessionId 매핑 삭제
            Task? delMappingTask = null;
            if (userIdValue.HasValue)
                delMappingTask = transaction.KeyDeleteAsync($"{UserSessionMappingKey}:{userIdValue}");

            var committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to remove session");
            
            if (delMappingTask is null)
                await Task.WhenAll(delSessionTask, removeActiveTask);
            else
                await Task.WhenAll(delSessionTask, removeActiveTask, delMappingTask);
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to remove session");
        }
    }

    /// <summary>
    /// 현재 활성 세션 수 조회
    /// </summary>
    public async Task<long> GetActiveSessionCountAsync()
    {
        return await _database.SetLengthAsync(ActiveSessionsKey);
    }

    /// <summary>
    /// 모든 활성 세션 조회
    /// </summary>
    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync()
    {
        // 활성 세션 Set에서 모든 SessionId 조회
        var sessionIds = await _database.SetMembersAsync(ActiveSessionsKey);

        if (sessionIds.Length == 0)
            return Enumerable.Empty<UserSession>();

        // Batch를 사용하여 Redis 요청 병렬 처리
        var batch = _database.CreateBatch();
        var tasks = sessionIds
            .Select(sessionId => batch.HashGetAllAsync($"{SessionKey}:{sessionId}"))
            .ToList();

        batch.Execute();

        var sessions = new List<UserSession>();
        for (int i = 0; i < sessionIds.Length; i++)
        {
            var entries = await tasks[i];
            if (entries.Length == 0)
                continue;

            var session = ParseSessionFromEntries(sessionIds[i].ToString(), entries);
            if (session is not null)
                sessions.Add(session);
        }

        return sessions;
    }

    /// <summary>
    /// TTL 만료로 사라진 세션을 Active Set에서 정리
    /// </summary>
    public async Task CleanupExpiredSessionsAsync(TimeSpan timeout)
    {
        try
        {
            // 활성 세션 목록 조회
            var sessionIds = await _database.SetMembersAsync(ActiveSessionsKey);

            if (sessionIds.Length == 0)
                return;

            // Batch로 실제 세션 키 존재 여부 확인
            var batch = _database.CreateBatch();
            var existsTasks = sessionIds
                .Select(sessionId => batch.KeyExistsAsync($"{SessionKey}:{sessionId}"))
                .ToList();

            batch.Execute();

            var transaction = _database.CreateTransaction();
            int expiredCount = 0;

            for (int i = 0; i < sessionIds.Length; i++)
            {
                bool exists = await existsTasks[i];
                if (!exists)
                {
                    // 실제 세션이 없는 경우 Active Set에서 제거
                    _ = transaction.SetRemoveAsync(ActiveSessionsKey, sessionIds[i]);
                    expiredCount++;
                }
            }

            if (expiredCount > 0)
            {
                await transaction.ExecuteAsync();
                Console.WriteLine($"Removed {expiredCount} expired sessions");
            }
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Failed to cleanup expired sessions");
        }
    }

    /// <summary>
    /// Redis HashEntry를 UserSession 도메인 객체로 변환
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
            !dict.TryGetValue("UserName", out var userName) ||
            !dict.TryGetValue("LoginAt", out var loginAtStr) ||
            !dict.TryGetValue("LastActiveAt", out var lastActiveAtStr))
        {
            Console.WriteLine($"Session {sessionId} has missing fields");
            return null;
        }

        // UserId 파싱
        if (!long.TryParse(userIdStr, out var userId))
        {
            Console.WriteLine($"Invalid UserId in session {sessionId}");
            return null;
        }

        // 날짜 파싱 (ISO 8601)
        if (!DateTime.TryParse(loginAtStr, null, DateTimeStyles.RoundtripKind, out var loginAt))
        {
            Console.WriteLine($"Invalid LoginAt in session {sessionId}");
            return null;
        }

        if (!DateTime.TryParse(lastActiveAtStr, null, DateTimeStyles.RoundtripKind, out var lastActiveAt))
        {
            Console.WriteLine($"Invalid LastActiveAt in session {sessionId}");
            return null;
        }

        return UserSession.FromRedis(sessionId, userId, userName, loginAt, lastActiveAt);
    }
}