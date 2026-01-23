namespace GameServer.Domain.Entities;

public class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime LoginAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    
    private UserSession(){ }
    
    public static UserSession Create(long userId, string userName, string sessionId)
    {
        if(string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be null or whitespace", nameof(sessionId));
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            UserName = userName,
            LoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
    }

    public static UserSession FromRedis(string sessionId, long userId, string userName,
        DateTime loginAt, DateTime lastActiveAt)
    {
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            UserName = userName,
            LoginAt = loginAt,
            LastActiveAt = lastActiveAt
        };
    }
}