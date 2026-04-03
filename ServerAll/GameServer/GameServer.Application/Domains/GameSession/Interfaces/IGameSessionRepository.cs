using GameSessionEntity = GameServer.Domain.Entities.GameSession.GameSession;

namespace GameServer.Application.Domains.GameSession.Interfaces;

public interface IGameSessionRepository
{
    Task<GameSessionEntity> CreateAsync(long roomId,string socketIp, int socketPort, CancellationToken ct = default);
    
    Task<GameSessionEntity?> GetAsync(long gameSessionId, CancellationToken ct = default);
    
    Task<GameSessionEntity?> GetByRoomIdAsync(long roomId, CancellationToken ct = default);
    
    Task<bool> UpdateAsync(GameSessionEntity gameSession, CancellationToken ct = default);
    
    Task<bool> RemoveAsync(long gameSessionId, CancellationToken ct = default);
}
