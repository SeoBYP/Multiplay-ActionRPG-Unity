using GameServer.Application.Common.MessageQueue;
using GameServer.Application.Domains.GameSession.Interfaces;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Application.Domains.GameSession;

public class GameSessionService(
    IGameSessionRepository gameSessionRepository,
    IGameSessionPlayerRepository gameSessionPlayerRepository,
    IMessageQueue<GameSessionReadyMessage> gameSessionReadyMessageQueue,
    ILogger<GameSessionService> logger) : IGameSessionService
{
    public async Task<Domain.Entities.GameSession.GameSession> CreateGameSessionAsync(
        long roomId,
        IReadOnlyCollection<long> playerIds,
        string host,
        int port,
        string traceId = "",
        CancellationToken ct = default)
    {
        if (roomId <= 0)
            throw new ArgumentException("RoomId invalid", nameof(roomId));
        if (playerIds.Count == 0)
            throw new ArgumentException("At least one player is required", nameof(playerIds));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host invalid", nameof(host));
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port), "Port invalid");

        try
        {
            var existingSession = await gameSessionRepository.GetByRoomIdAsync(roomId, ct);
            if (existingSession is not null)
            {
                await gameSessionReadyMessageQueue.EnqueueAsync(new GameSessionReadyMessage
                {
                    RoomId = roomId,
                    GameSessionId = existingSession.GameSessionId,
                    Host = existingSession.SocketIp,
                    Port = existingSession.SocketPort,
                    TraceId = traceId
                });

                logger.LogInformation(
                    "Game session already exists for room {RoomId}. Reusing session {GameSessionId}",
                    roomId,
                    existingSession.GameSessionId);
                return existingSession;
            }

            var gameSession = await gameSessionRepository.CreateAsync(roomId, host, port, ct);
            foreach (var playerId in playerIds.Distinct())
            {
                await gameSessionPlayerRepository.CreateAsync(gameSession.GameSessionId, playerId, ct);
            }

            await gameSessionReadyMessageQueue.EnqueueAsync(new GameSessionReadyMessage
            {
                RoomId = roomId,
                GameSessionId = gameSession.GameSessionId,
                Host = host,
                Port = port,
                TraceId = traceId
            });

            logger.LogInformation(
                "Created game session {GameSessionId} for room {RoomId} with {PlayerCount} players",
                gameSession.GameSessionId,
                roomId,
                playerIds.Count);
            return gameSession;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create game session for room {RoomId}", roomId);
            throw;
        }
    }
}
