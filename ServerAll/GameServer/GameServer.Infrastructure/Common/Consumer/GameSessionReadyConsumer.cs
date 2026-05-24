using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Infrastructure.Domains.GameSession;

public sealed class GameSessionReadyConsumer(
    IMessageQueue<GameSessionReadyMessage> gameSessionReadyMessageQueue,
    IServiceScopeFactory scopeFactory,
    IDungeonLobbySubscriptionService subscriptionService,
    ILogger<GameSessionReadyConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in gameSessionReadyMessageQueue.DequeueAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) return;

                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var gameSessionService = scope.ServiceProvider.GetRequiredService<IGameSessionService>();
                    var roomRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomRepository>();
                    var roomPlayerRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomPlayerRepository>();

                    var room = await roomRepository.GetByIdAsync(message.RoomId, stoppingToken);
                    if (room is null)
                    {
                        logger.LogWarning("Room {RoomId} was not found while handling game session ready", message.RoomId);
                        continue;
                    }

                    var players = await roomPlayerRepository.GetPlayersByRoomIdAsync(message.RoomId, stoppingToken);

                    await gameSessionService.CreateGameSessionAsync(
                        message.RoomId,
                        players.Select(player => player.UserId).ToList(),
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
                    return;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Failed to process game session ready for room {RoomId}", message.RoomId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 서버 종료 시 정상 취소 — 호스트로 전파하지 않는다
        }
        catch (Exception e)
        {
            logger.LogError(e, "GameSessionReadyConsumer loop failed unexpectedly");
        }
    }
}
