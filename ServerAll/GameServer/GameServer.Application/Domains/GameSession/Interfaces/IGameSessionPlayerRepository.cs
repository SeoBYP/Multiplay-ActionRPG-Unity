using GameSessionPlayerEntity = GameServer.Domain.Entities.GameSession.GameSessionPlayer;

namespace GameServer.Application.Domains.GameSession.Interfaces;

public interface IGameSessionPlayerRepository
{
    Task<GameSessionPlayerEntity> CreateAsync(long gameSessionId, long userId, CancellationToken ct = default);
    
    Task<List<GameSessionPlayerEntity>> GetPlayersByGameSessionIdAsync(long gameSessionId, CancellationToken ct = default);
    
    Task<GameSessionPlayerEntity?> GetByUserIdAsync(long userId, CancellationToken ct = default);
    
    Task<bool> UpdateAsync(GameSessionPlayerEntity gameSessionPlayer, CancellationToken ct = default);
    
    Task<bool> RemoveAsync(long gameSessionId, long userId, CancellationToken ct = default);
}
