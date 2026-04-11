using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;
using Server.Infrastructure;
using Server.Room;
using Shared.Infrastructure.Messages;

namespace Server.Consumer;

public class GameStartRequestedConsumer(
    GameStartRequestedMessageQueue gameStartQueue,
    GameSessionReadyMessageQueue gameSessionReadyQueue,
    RoomManager roomManager,
    IOptions<ServerOptions> serverOptions,
    ILogger<GameStartRequestedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = serverOptions.Value;
        try
        {
            await foreach (var msg in gameStartQueue.DequeueAllAsync(stoppingToken))
            {
                using (LogContext.PushProperty("TraceId", msg.TraceId))
                using (LogContext.PushProperty("RoomId", msg.RoomId))
                {
                    try
                    {
                        var room = roomManager.CreateRoom(msg.RoomId, msg.PlayerInfos, msg);
                        if (room is null)
                        {
                            logger.LogWarning("[GameStart] Room creation skipped for RoomId={RoomId}", msg.RoomId);
                            continue;
                        }

                        logger.LogInformation("[GameStart] RoomId={RoomId}, Players={PlayerCount}명", msg.RoomId, msg.PlayerInfos.Count);

                        await gameSessionReadyQueue.EnqueueAsync(new GameSessionReadyMessage
                        {
                            RoomId = msg.RoomId,
                            GameSessionId = 0,
                            Host = options.Ip,
                            Port = options.Port,
                            TraceId = msg.TraceId
                        });
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Error processing GameStartRequestedMessage for RoomId={RoomId}", msg.RoomId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("GameStartRequestedConsumer operation canceled.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Fatal error in GameStartRequestedConsumer loop");
        }
    }
}
