using GameServer.Domain.Entities;

namespace GameServer.Domain.Interfaces;

public interface ISessionRepository
{
    Task<UserSession?> CreateSessionAsync(long userId, string userName);
    Task<UserSession?> GetBySessionIdAsync(string sessionId);
    Task<UserSession?> GetSessionByUserIdAsync(long userId);
    
    Task RemoveSessionAsync(string sessionId);
    Task<long> GetActiveSessionCountAsync();
    Task<IEnumerable<UserSession>> GetActiveSessionsAsync();
    Task CleanupExpiredSessionsAsync(TimeSpan timeout);
}