using GameSessionEntity = GameServer.Domain.Entities.GameSession.GameSession;

namespace GameServer.Application.Domains.GameSession;

public interface IGameSessionService
{
    Task<GameSessionEntity> CreateGameSessionAsync(
        long roomId,
        IReadOnlyCollection<long> playerIds,
        string host,
        int port,
        string traceId = "",
        CancellationToken ct = default);
}
