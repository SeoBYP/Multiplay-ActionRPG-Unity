namespace GameServer.Domain.Entities;

public class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastActiveAt { get; set; }

    private UserSession()
    {
    }

    public static UserSession Create(long userId, string sessionId)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero", nameof(userId));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be null or whitespace", nameof(sessionId));

        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            LoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
    }

    /// <summary>이 세션이 방금 활동했음을 기록한다(생존 신호).</summary>
    public void Touch() => LastActiveAt = DateTime.UtcNow;

    public static UserSession Restore(string sessionId, long userId, DateTime loginAt, DateTime lastActiveAt)
    {
        if (userId <= 0)
            throw new ArgumentException("User id must be greater than zero", nameof(userId));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be null or whitespace", nameof(sessionId));

        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            LoginAt = loginAt,
            LastActiveAt = lastActiveAt
        };
    }
}
