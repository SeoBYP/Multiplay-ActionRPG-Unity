using GameServer.Application.Domains.GameSession.Interfaces;
using GameServer.Domain.Entities.GameSession;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public sealed class FakeGameSessionPlayerRepository : IGameSessionPlayerRepository
{
    public Task<GameSessionPlayer> CreateAsync(long gameSessionId, long userId, CancellationToken ct = default)
        => Task.FromResult(GameSessionPlayer.Create(gameSessionId, userId));

    public Task<List<GameSessionPlayer>> GetPlayersByGameSessionIdAsync(long gameSessionId, CancellationToken ct = default)
        => Task.FromResult(new List<GameSessionPlayer>());

    public Task<GameSessionPlayer?> GetByUserIdAsync(long userId, CancellationToken ct = default)
        => Task.FromResult<GameSessionPlayer?>(null);

    public Task<bool> UpdateAsync(GameSessionPlayer gameSessionPlayer, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<bool> RemoveAsync(long gameSessionId, long userId, CancellationToken ct = default)
        => Task.FromResult(true);
}
