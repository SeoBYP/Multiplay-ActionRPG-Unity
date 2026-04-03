using GameServer.Application.Domains.DungeonLobby.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

public class RedisSocketReadyChecker(
    IConnectionMultiplexer redis,
    ILogger<RedisSocketReadyChecker> logger) : ISocketReadyChecker
{
    public async Task<SocketEndpoint?> WaitForReadyAsync(long roomId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            var val = await db.StringGetAsync($"socket:room:{roomId}:ready");
            if (val.HasValue)
            {
                var rawValue = val.ToString();
                logger.LogInformation("Socket ready for room {RoomId}: {SocketInfo}", roomId, rawValue);
                return Parse(rawValue);
            }

            await Task.Delay(100, ct);
        }

        logger.LogWarning("Socket ready timed out for room {RoomId}", roomId);
        return null;
    }

    private static SocketEndpoint Parse(string rawValue)
    {
        var separatorIndex = rawValue.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == rawValue.Length - 1)
            throw new FormatException($"Invalid socket endpoint format: {rawValue}");

        var host = rawValue[..separatorIndex];
        var portText = rawValue[(separatorIndex + 1)..];
        if (!int.TryParse(portText, out var port))
            throw new FormatException($"Invalid socket port: {rawValue}");

        return new SocketEndpoint(host, port);
    }
}
