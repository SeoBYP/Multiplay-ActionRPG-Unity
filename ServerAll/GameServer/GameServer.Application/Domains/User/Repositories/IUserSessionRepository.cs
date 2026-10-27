using GameServer.Domain.Entities;

namespace GameServer.Application.Domains.User.Interfaces;

public interface IUserSessionRepository
{
    /// <returns>생성된 세션 (실패 시 null)</returns>
    Task<UserSession?> CreateSessionAsync(long userId, CancellationToken ct = default);

    Task<UserSession?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);

    Task<UserSession?> GetSessionByUserIdAsync(long userId, CancellationToken ct = default);
    
    Task RemoveSessionAsync(string sessionId, CancellationToken ct = default);

    Task<long> GetActiveSessionCountAsync(CancellationToken ct = default);

    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(CancellationToken ct = default);

    /// <summary>세션 활동(생존 신호)을 기록한다. 인증된 RPC 마다 호출되므로 구현이 스로틀한다 — 남은 수명이 절반 이상이면 쓰지 않는다.</summary>
    Task TouchSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>세션 활성 집합의 만료 시각(UTC). 세션이 없거나 활성 집합에 없으면 null.</summary>
    /// <remarks>"최근 인증 활동 시각 + AccessToken 수명"의 근사값이며, 현재 유일한 생존 근사 신호다(정식 하트비트 없음).</remarks>
    Task<DateTime?> GetSessionActiveUntilAsync(long userId, CancellationToken ct = default);

    Task CleanupExpiredSessionsAsync(TimeSpan timeout);
}
