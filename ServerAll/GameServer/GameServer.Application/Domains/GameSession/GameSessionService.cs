using GameServer.Application.Domains.GameSession.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameServer.Application.Domains.GameSession;

public class GameSessionService(
    IGameSessionRepository gameSessionRepository,
    IGameSessionPlayerRepository gameSessionPlayerRepository,
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
                if (existingSession.SocketIp != host || existingSession.SocketPort != port)
                {
                    logger.LogInformation(
                        "Updating game session {GameSessionId} socket info from {OldIp}:{OldPort} to {NewIp}:{NewPort}",
                        existingSession.GameSessionId, existingSession.SocketIp, existingSession.SocketPort, host, port);
                    existingSession.UpdateSocketInfo(host, port);
                    await gameSessionRepository.UpdateAsync(existingSession, ct);
                }
                else
                {
                    logger.LogInformation(
                        "Game session already exists for room {RoomId}. Reusing session {GameSessionId}",
                        roomId, existingSession.GameSessionId);
                }
                return existingSession;
            }

            var gameSession = await gameSessionRepository.CreateAsync(roomId, host, port, ct);
            foreach (var playerId in playerIds.Distinct())
            {
                await gameSessionPlayerRepository.CreateAsync(gameSession.GameSessionId, playerId, ct);
            }

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
