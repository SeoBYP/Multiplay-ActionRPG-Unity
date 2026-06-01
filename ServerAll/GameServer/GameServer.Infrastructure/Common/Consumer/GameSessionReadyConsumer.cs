using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.GameSession;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.GameSession;

public sealed class GameSessionReadyConsumer(
    IMessageQueue<GameSessionReadyMessage> gameSessionReadyMessageQueue,
    IConnectionMultiplexer connectionMultiplexer,
    IServiceScopeFactory scopeFactory,
    IDungeonLobbySubscriptionService subscriptionService,
    ILogger<GameSessionReadyConsumer> logger) : BackgroundService
{
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private static readonly TimeSpan PlayerDataTtl = TimeSpan.FromHours(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in gameSessionReadyMessageQueue.DequeueAllAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) return;

                try
                {
                    logger.LogInformation("[GameSessionReadyConsumer] 처리 시작 — RoomId={RoomId} Host={Host} Port={Port} TraceId={TraceId}",
                        message.RoomId, message.Host, message.Port, message.TraceId);

                    using var scope = scopeFactory.CreateScope();
                    var gameSessionService = scope.ServiceProvider.GetRequiredService<IGameSessionService>();
                    var roomRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomRepository>();
                    var roomPlayerRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomPlayerRepository>();
                    var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

                    var room = await roomRepository.GetByIdAsync(message.RoomId, stoppingToken);
                    if (room is null)
                    {
                        logger.LogWarning("[GameSessionReadyConsumer] RoomId={RoomId} — 방 없음, 스킵", message.RoomId);
                        continue;
                    }

                    logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} 현재 상태={Status}",
                        message.RoomId, room.Status);

                    var players = await roomPlayerRepository.GetPlayersByRoomIdAsync(message.RoomId, stoppingToken);
                    logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} 플레이어 수={Count}",
                        message.RoomId, players.Count);

                    var gameSession = await gameSessionService.CreateGameSessionAsync(
                        message.RoomId,
                        players.Select(player => player.UserId).ToList(),
                        message.Host,
                        message.Port,
                        message.TraceId,
                        stoppingToken);

                    logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} GameSession={GameSessionId}",
                        message.RoomId, gameSession.GameSessionId);

                    if (room.Status == RoomStatus.Starting)
                    {
                        room.MarkGameSessionReady();
                        var updated = await roomRepository.UpdateAsync(room, stoppingToken);
                        if (!updated)
                        {
                            logger.LogWarning("[GameSessionReadyConsumer] RoomId={RoomId} — UpdateAsync 실패, 스킵", message.RoomId);
                            continue;
                        }
                        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} — Playing으로 업데이트 완료", message.RoomId);
                    }
                    else
                    {
                        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} — 상태={Status}, MarkGameSessionReady 스킵",
                            message.RoomId, room.Status);
                    }

                    for (int i = 0; i < players.Count; i++)
                    {
                        var player = players[i];
                        var profile = await userProfileRepository.GetByIdAsync(player.UserId, stoppingToken);
                        var nickname = profile?.NickName ?? $"Player_{player.UserId}";

                        var key = $"gamesession:player:{player.UserId}";
                        var entries = new HashEntry[]
                        {
                            new("roomId", message.RoomId),
                            new("gameSessionId", gameSession.GameSessionId),
                            new("nickname", nickname),
                            new("spawnIndex", i)
                        };
                        await _redis.HashSetAsync(key, entries);
                        await _redis.KeyExpireAsync(key, PlayerDataTtl);

                        logger.LogInformation(
                            "[GameSessionReady] Saved player data to Redis: UserId={UserId} RoomId={RoomId} SpawnIndex={SpawnIndex}",
                            player.UserId, message.RoomId, i);
                    }

                    logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} — PublishAsync 호출", message.RoomId);
                    await subscriptionService.PublishAsync(message.RoomId, stoppingToken);
                    logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} — 처리 완료", message.RoomId);
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
