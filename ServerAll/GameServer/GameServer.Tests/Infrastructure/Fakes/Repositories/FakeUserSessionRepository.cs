using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeUserSessionRepository : IUserSessionRepository
{
    private readonly Dictionary<string, UserSession> _sessions = new();
    private readonly Dictionary<long, string> _userToSession = new();
    private readonly Dictionary<long, DateTime> _activeUntil = new();

    /// <summary>테스트가 "이 유저의 마지막 활동 신호"를 직접 세팅한다.</summary>
    public void SetActiveUntil(long userId, DateTime? activeUntil)
    {
        if (activeUntil is null) _activeUntil.Remove(userId);
        else _activeUntil[userId] = activeUntil.Value;
    }

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

    public Task TouchSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            _activeUntil[session.UserId] = DateTime.UtcNow.AddMinutes(15);

        return Task.CompletedTask;
    }

    public Task<DateTime?> GetSessionActiveUntilAsync(long userId, CancellationToken ct = default)
    {
        if (_activeUntil.TryGetValue(userId, out var until))
            return Task.FromResult<DateTime?>(until);

        // 세션을 만든 적이 없으면 신호 자체가 없다.
        if (!_userToSession.ContainsKey(userId))
            return Task.FromResult<DateTime?>(null);

        // 실제 구현과 같은 기본값: 생성 시점에 만료 시각이 찍힌다.
        return Task.FromResult<DateTime?>(DateTime.UtcNow.AddMinutes(15));
    }

    public Task CleanupExpiredSessionsAsync(TimeSpan timeout)
    {
        return Task.CompletedTask;
    }
}