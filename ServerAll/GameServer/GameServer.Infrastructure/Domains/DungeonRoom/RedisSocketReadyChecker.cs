using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class RedisSocketReadyChecker(
    IConnectionMultiplexer redis,
    ILogger<RedisSocketReadyChecker> logger) : ISocketReadyChecker
{
    public async Task<string?> WaitAsync(long roomId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        // TODO : 임시로 10초의 대기 시간을 갖는다.
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var val = await db.StringGetAsync($"socket:room:{roomId}:ready");
            if (val.HasValue)
            {
                logger.LogInformation("Socket ready for room {RoomId}: {SocketInfo}", roomId, val.ToString());
                return val.ToString();
            }
            await Task.Delay(100, ct);
        }
        logger.LogWarning("Socket ready timed out for room {RoomId}", roomId);
        return null;
    }
}
