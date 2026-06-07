using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeUserSessionRepository : IUserSessionRepository
{
    private readonly Dictionary<string, UserSession> _sessions = new();
    private readonly Dictionary<long, string> _userToSession = new();

    public Task<UserSession?> CreateSessionAsync(long userId, CancellationToken ct = default)
    {
        // 실제 UserSessionRepository와 동일: 새 세션 생성 전 기존 세션 제거(단일 세션 강제).
        if (_userToSession.TryGetValue(userId, out var existingSessionId))
            _sessions.Remove(existingSessionId);

        var sessionId = Guid.NewGuid().ToString();
        var session = UserSession.Create(userId, sessionId);

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

    public Task RemoveSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.Remove(sessionId, out var session))
        {
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

    public Task CleanupExpiredSessionsAsync(TimeSpan timeout)
    {
        return Task.CompletedTask;
    }
}