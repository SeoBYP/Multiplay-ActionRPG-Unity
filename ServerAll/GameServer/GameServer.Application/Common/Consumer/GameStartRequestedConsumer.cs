using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Application.Common.Consumer;

public sealed class GameStartRequestedConsumer(
    IMessageQueue<GameStartRequestedMessage> gameStartRequestedMessageQueue,
    IGameStartPublisher gameStartPublisher,
    ISocketReadyChecker socketReadyChecker,
    IGameSessionService gameSessionService,
    ILogger<GameStartRequestedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in gameStartRequestedMessageQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                await gameStartPublisher.PublishGameStartAsync(message, stoppingToken);

                var socketEndpoint = await socketReadyChecker.WaitForReadyAsync(message.RoomId, stoppingToken);
                if (socketEndpoint is null)
                {
                    logger.LogWarning("Socket endpoint was not ready for room {RoomId}", message.RoomId);
                    continue;
                }

                await gameSessionService.CreateGameSessionAsync(
                    message.RoomId,
                    message.PlayerIds,
                    socketEndpoint.Host,
                    socketEndpoint.Port,
                    message.TraceId,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to process game start request for room {RoomId}", message.RoomId);
            }
        }
    }
}
