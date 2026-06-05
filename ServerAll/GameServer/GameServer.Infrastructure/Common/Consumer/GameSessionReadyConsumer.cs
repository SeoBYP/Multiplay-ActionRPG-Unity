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

    // 복원력(일시적 Redis 오류에 죽지 않음)은 ResilientStreamConsumer 공통 루프가 담당.
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => ResilientStreamConsumer.RunAsync<GameSessionReadyMessage>(
            nameof(GameSessionReadyConsumer),
            gameSessionReadyMessageQueue.DequeueAllAsync,
            ProcessAsync,
            logger,
            stoppingToken);

    private async Task ProcessAsync(GameSessionReadyMessage message, CancellationToken ct)
    {
        logger.LogInformation("[GameSessionReadyConsumer] 처리 시작 — RoomId={RoomId} Host={Host} Port={Port} TraceId={TraceId}",
            message.RoomId, message.Host, message.Port, message.TraceId);

        using var scope = scopeFactory.CreateScope();
        var gameSessionService = scope.ServiceProvider.GetRequiredService<IGameSessionService>();
        var roomRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomRepository>();
        var roomPlayerRepository = scope.ServiceProvider.GetRequiredService<IDungeonRoomPlayerRepository>();
        var userProfileRepository = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();

        var room = await roomRepository.GetByIdAsync(message.RoomId, ct);
        if (room is null)
        {
            logger.LogWarning("[GameSessionReadyConsumer] RoomId={RoomId} — 방 없음, 스킵", message.RoomId);
            return;
        }

        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} 현재 상태={Status}",
            message.RoomId, room.Status);

        var players = await roomPlayerRepository.GetPlayersByRoomIdAsync(message.RoomId, ct);
        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} 플레이어 수={Count}",
            message.RoomId, players.Count);

        var gameSession = await gameSessionService.CreateGameSessionAsync(
            message.RoomId,
            players.Select(player => player.UserId).ToList(),
            message.Host,
            message.Port,
            message.TraceId,
            ct);

        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} GameSession={GameSessionId}",
            message.RoomId, gameSession.GameSessionId);

        if (room.Status == RoomStatus.Starting)
        {
            room.MarkGameSessionReady();
            var updated = await roomRepository.UpdateAsync(room, ct);
            if (!updated)
            {
                logger.LogWarning("[GameSessionReadyConsumer] RoomId={RoomId} — UpdateAsync 실패, 스킵", message.RoomId);
                return;
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
            var profile = await userProfileRepository.GetByIdAsync(player.UserId, ct);
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
        await subscriptionService.PublishAsync(message.RoomId, ct);
        logger.LogInformation("[GameSessionReadyConsumer] RoomId={RoomId} — 처리 완료", message.RoomId);
    }
}
