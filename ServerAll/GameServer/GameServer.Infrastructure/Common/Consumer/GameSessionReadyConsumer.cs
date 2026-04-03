using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Domains.GameSession;

public sealed class GameSessionReadyConsumer(
    IMessageQueue<GameSessionReadyMessage> gameSessionReadyMessageQueue,
    IGameSessionService gameSessionService,
    IDungeonRoomRepository roomRepository,
    IDungeonLobbySubscriptionService subscriptionService,
    ILogger<GameSessionReadyConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in gameSessionReadyMessageQueue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                var room = await roomRepository.GetByIdAsync(message.RoomId, stoppingToken);
                if (room is null)
                {
                    logger.LogWarning("Room {RoomId} was not found while handling game session ready", message.RoomId);
                    continue;
                }

                await gameSessionService.CreateGameSessionAsync(
                    message.RoomId,
                    room.CurrentPlayers.ToList(),
                    message.Host,
                    message.Port,
                    message.TraceId,
                    stoppingToken);

                if (room.Status == RoomStatus.Starting)
                {
                    room.MarkGameSessionReady();
                    var updated = await roomRepository.UpdateAsync(room, stoppingToken);
                    if (!updated)
                    {
                        logger.LogWarning("Failed to update room {RoomId} to playing status", message.RoomId);
                        continue;
                    }
                }

                await subscriptionService.PublishAsync(message.RoomId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to process game session ready for room {RoomId}", message.RoomId);
            }
        }
    }
}
