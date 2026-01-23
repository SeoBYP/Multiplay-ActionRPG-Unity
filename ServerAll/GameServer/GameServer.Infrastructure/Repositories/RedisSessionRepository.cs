using System.Collections.Concurrent;
using System.Globalization;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories;

public class RedisSessionRepository(IConnectionMultiplexer connectionMultiplexer) : ISessionRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string SessionKey = "game:session";
    private const string ActiveSessionsKey = "game:session:active";
    private const string UserSessionMappingKey = "game:user:session";
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(1);

    public async Task<UserSession?> CreateSessionAsync(long userId, string userName)
    {
        try
        {
            var ttl = TimeSpan.FromHours(1);

            var transaction = _database.CreateTransaction();

            var sessionId = Guid.CreateVersion7().ToString();
            var newSession = UserSession.Create(userId, userName, sessionId);

            // redis�� ���� ���� ����
            await transaction.HashSetAsync($"{SessionKey}:{sessionId}",
            [
                new HashEntry("UserId", userId),
                new HashEntry("UserName", userName),
                new HashEntry("LoginAt", newSession.LoginAt.ToString("O")),
                new HashEntry("LastActiveAt", newSession.LastActiveAt.ToString("O"))
            ]);

            // ���� ���� Time to Live(TTL) ����
            await transaction.KeyExpireAsync($"{SessionKey}:{sessionId}", ttl);

            // session:active set�� sessionId �߰� => Ȱ��ȭ�� Action Count
            await transaction.SetAddAsync(ActiveSessionsKey, sessionId);

            // UserId�� SessionId�� ���� => UserId ��� Session ��������
            await transaction.StringSetAsync($"{UserSessionMappingKey}:{userId}", sessionId, ttl);

            bool commited = await transaction.ExecuteAsync();
            if (!commited)
            {
                throw new InvalidOperationException("Failed to create session");
            }

            return newSession;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to create session");
        }
    }

    public async Task<UserSession?> GetBySessionIdAsync(string sessionId)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{SessionKey}:{sessionId}");
            if (entries.Length == 0)
                return null;
            return ParseSessionFromEntries(sessionId, entries);
        }
        catch (RedisException ex)
        {
            // Redis ���� ���ܴ� �α� �� null ��ȯ
            Console.WriteLine($"Redis error while getting session {sessionId}");
            return null;
        }
        catch (Exception ex)
        {
            // ����ġ ���� ���ܴ� ������ ����
            Console.WriteLine($"Unexpected error while getting session {sessionId}");
            throw;
        }
    }

    public async Task<UserSession?> GetSessionByUserIdAsync(long userId)
    {
        var sessionId = await _database.StringGetAsync($"{UserSessionMappingKey}:{userId}");
        // ���� ������ null
        if (!sessionId.HasValue)
            return null;
        // ��ȿ���� ���� ���̸� null
        if (string.IsNullOrWhiteSpace(sessionId.ToString()))
            return null;
        return await GetBySessionIdAsync(sessionId.ToString());
    }

    public async Task RemoveSessionAsync(string sessionId)
    {
        try
        {
            var transaction = _database.CreateTransaction();

            var userIdValue = await transaction.HashGetAsync($"{SessionKey}:{sessionId}", "UserId");
            // Session ����
            await transaction.KeyDeleteAsync($"{SessionKey}:{sessionId}");
            // sessions:active���� ����
            await transaction.SetRemoveAsync(ActiveSessionsKey, sessionId);
            // UserId ���� ���� (UserId ������)
            if (userIdValue.HasValue)
            {
                await transaction.KeyDeleteAsync($"{UserSessionMappingKey}:{userIdValue}");
            }

            bool commited = await transaction.ExecuteAsync();
            if (!commited)
            {
                throw new InvalidOperationException("Failed to remove session");
            }
        }
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to remove session");
        }
    }

    public async Task<long> GetActiveSessionCountAsync()
    {
        return await _database.SetLengthAsync(ActiveSessionsKey);
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync()
    {
        // session:active�� ��� SessionId ��������
        var sessionIds = await _database.SetMembersAsync(ActiveSessionsKey);

        if (sessionIds.Length == 0)
            return Enumerable.Empty<UserSession>();

        // Batch�� ����Ͽ� ���� ó���� �Ѵ�.
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

    public async Task CleanupExpiredSessionsAsync(TimeSpan timeout)
    {
        try
        {
            // session:active�� ��� sessionId ��������
            var sessionIds = await _database.SetMembersAsync(ActiveSessionsKey);

            if (sessionIds.Length == 0)
                return;

            // Batch�� ���� ���� Ȯ��
            var batch = _database.CreateBatch();
            var existsTasks = sessionIds
                .Select(sessionId => batch.KeyExistsAsync($"{SessionKey}:{sessionId}"))
                .ToList();

            batch.Execute();

            // ����� ���� ����
            var transaction = _database.CreateTransaction();
            int expiredCount = 0;

            for (int i = 0; i < sessionIds.Length; i++)
            {
                bool exists = await existsTasks[i];
                if (!exists)
                {
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
        catch (Exception e)
        {
            throw new InvalidOperationException("Failed to cleanup expired sessions");
        }
    }


    private UserSession? ParseSessionFromEntries(string sessionId, HashEntry[] entries)
    {
        // HashEntry�� Dictionary�� ��ȯ
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        // �ʼ� �ʵ� ����
        if (!dict.TryGetValue("UserId", out var userIdStr) ||
            !dict.TryGetValue("UserName", out var userName) ||
            !dict.TryGetValue("LoginAt", out var loginAtStr) ||
            !dict.TryGetValue("LastActiveAt", out var lastActiveAtStr))
        {
            Console.WriteLine($"Session {sessionId} has missing fields");
            return null;
        }

        // ������ �Ľ�
        if (!long.TryParse(userIdStr, out var userId))
        {
            Console.WriteLine($"Invalid UserId in session {sessionId}");
            return null;
        }

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
