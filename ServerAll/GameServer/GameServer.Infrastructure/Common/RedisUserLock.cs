using GameServer.Application.Common.Interfaces;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Common;

public class RedisUserLock(IConnectionMultiplexer connectionMultiplexer) : IUserLock
{
    private const string LockPrefix = "lock:user:";
    
    // TODO : application.json으로 분류 예정
    private static readonly TimeSpan LockExpiry = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan DeadLine = TimeSpan.FromSeconds(5);

    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();
    
    public async Task<IAsyncDisposable> AcquireAsync(string lockKey, CancellationToken ct = default)
    {
        var key = $"{LockPrefix}:{lockKey}";
        var token = Guid.NewGuid().ToString("N"); // 소유자 식별용
        
        // 락 획득 시도 (최대 5초)
        var deadline = DateTime.UtcNow.Add(DeadLine);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            // SET key token NX EX 10
            var acquired = await _database.StringSetAsync(key, token, LockExpiry, When.NotExists);
            if (acquired)
                return new RedisLockHandle(_database, key, token);

            await Task.Delay(RetryInterval, ct);
        }
        throw new TimeoutException($"Failed to acquire lock for key: {lockKey}");
    }
    
    private sealed class RedisLockHandle(IDatabase db, string key, string token) : IAsyncDisposable
    {
        // Lua 스크립트: 내가 건 락만 해제 (타인 락 건드리지 않음)
        private static readonly string ReleaseLua = """
                                                    if redis.call("get", KEYS[1]) == ARGV[1] then
                                                        return redis.call("del", KEYS[1])
                                                    else
                                                        return 0
                                                    end
                                                    """;

        public async ValueTask DisposeAsync()
        {
            await db.ScriptEvaluateAsync(ReleaseLua,
                keys: [key],
                values: [token]);
        }
    }
}