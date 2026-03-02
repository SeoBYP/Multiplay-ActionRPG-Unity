namespace GameServer.Tests.Infrastructure;

using GameServer.Domain.Entities;
using GameServer.Infrastructure.Interfaces.User;

public class FakeUserSessionRepository : IUserSessionRepository
{
    private readonly Dictionary<string, UserSession> _sessions = new();
    private readonly Dictionary<long, string> _userToSession = new();

    public Task<UserSession?> CreateSessionAsync(long userId, string userName, string userEmail, string publicId, CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = UserSession.Create(userId, userEmail, userName, publicId, sessionId);
        
        _sessions[sessionId] = session;
        _userToSession[userId] = sessionId;
        
        return Task.FromResult<UserSession?>(session);
    }

    public Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<UserSession?> GetSessionByUserIdAsync(long userId, CancellationToken ct = default)
    {
        if (_userToSession.TryGetValue(userId, out var sessionId))
        {
            return GetBySessionIdAsync(sessionId, ct);
        }
        return Task.FromResult<UserSession?>(null);
    }

    public Task UpdateRoomIdAsync(string sessionId, long roomId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.SetRoomId(roomId);
        }
        return Task.CompletedTask;
    }

    public Task RemoveSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            _sessions.Remove(sessionId);
            _userToSession.Remove(session.UserId);
        }
        return Task.CompletedTask;
    }

    public Task<long> GetActiveSessionCountAsync(CancellationToken ct = default)
    {
        return Task.FromResult((long)_sessions.Count);
    }

    public Task<IEnumerable<UserSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_sessions.Values.AsEnumerable());
    }

    public Task CleanupExpiredSessionsAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}