using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Server.Infrastructure;
using Server.Room;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace Server.Consumer;

public class GameStartRequestedConsumer(
    GameStartRequestedMessageQueue gameStartQueue,
    GameSessionReadyMessageQueue gameSessionReadyQueue,
    RoomManager roomManager,
    IOptions<ServerOptions> serverOptions,
    ILogger<GameStartRequestedConsumer> logger) : BackgroundService
{
    // 복원력(일시적 Redis 오류에 죽지 않음)은 ResilientStreamConsumer 공통 루프가 담당.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<GameStartRequestedMessage>(
            nameof(GameStartRequestedConsumer),
            gameStartQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private async Task ProcessAsync(GameStartRequestedMessage msg, CancellationToken ct)
    {
        using (LogContext.PushProperty("TraceId", msg.TraceId))
        using (LogContext.PushProperty("RoomId", msg.RoomId))
        {
            var options = serverOptions.Value;

            var room = roomManager.CreateRoom(msg.RoomId, msg.PlayerInfos, msg);
            if (room is null)
            {
                // Room already exists — idempotent retry; still notify GameServer
                logger.LogWarning("[GameStart] Room already exists for RoomId={RoomId}, re-publishing GameSessionReady", msg.RoomId);
            }
            else
            {
                logger.LogInformation("[GameStart] RoomId={RoomId}, Players={PlayerCount}명", msg.RoomId, msg.PlayerInfos.Count);
            }

            var advertiseIp = options.ResolvedAdvertiseIp;
            logger.LogInformation(
                "[GameStart] GameSessionReady 발행: RoomId={RoomId} Host={Host} Port={Port}",
                msg.RoomId, advertiseIp, options.Port);

            await gameSessionReadyQueue.EnqueueAsync(new GameSessionReadyMessage
            {
                RoomId = msg.RoomId,
                GameSessionId = 0,
                Host = advertiseIp,
                Port = options.Port,
                TraceId = msg.TraceId
            });
        }
    }
}
