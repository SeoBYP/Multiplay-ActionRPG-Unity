using System.Collections.Concurrent;
using GameServer.Application.Domains.GameSession.Interfaces;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public sealed class FakeGameSessionRepository : IGameSessionRepository
{
    private readonly ConcurrentDictionary<long, GameServer.Domain.Entities.GameSession.GameSession> _sessions = new();
    private long _nextId = 1;

    public Task<GameServer.Domain.Entities.GameSession.GameSession> CreateAsync(long roomId, string socketIp, int socketPort, CancellationToken ct = default)
    {
        var session = GameServer.Domain.Entities.GameSession.GameSession.Create(roomId, socketIp, socketPort);
        session.SetId(Interlocked.Increment(ref _nextId));
        _sessions[roomId] = session;
        return Task.FromResult(session);
    }

    public Task<GameServer.Domain.Entities.GameSession.GameSession?> GetAsync(long gameSessionId, CancellationToken ct = default)
        => Task.FromResult(_sessions.Values.FirstOrDefault(x => x.GameSessionId == gameSessionId));

    public Task<GameServer.Domain.Entities.GameSession.GameSession?> GetByRoomIdAsync(long roomId, CancellationToken ct = default)
        => Task.FromResult(_sessions.TryGetValue(roomId, out var session) ? session : null);

    public Task<bool> UpdateAsync(GameServer.Domain.Entities.GameSession.GameSession gameSession, CancellationToken ct = default)
    {
        _sessions[gameSession.RoomId] = gameSession;
        return Task.FromResult(true);
    }

    public Task<bool> RemoveAsync(long gameSessionId, CancellationToken ct = default)
    {
        var roomId = _sessions.FirstOrDefault(x => x.Value.GameSessionId == gameSessionId).Key;
        return Task.FromResult(roomId != 0 && _sessions.TryRemove(roomId, out _));
    }
}
