using GameServer.Application.Domains.DungeonLobby.Interfaces;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class RedisSocketReadyChecker(IConnectionMultiplexer redis) : ISocketReadyChecker
{
    public async Task<string?> WaitAsync(long roomId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        // TODO : 임시로 10초의 대기 시간을 갖는다.
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var val = await db.StringGetAsync($"socket:room:{roomId}:ready");
            if (val.HasValue) return val.ToString();
            await Task.Delay(100, ct);
        }
        return null;
    }
}