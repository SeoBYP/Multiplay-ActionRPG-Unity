namespace GameServer.Domain.Entities;

public class UserSession
{
    public string SessionId { get; set; }
    public long UserId { get; set; }
    public string Email { get; set; } 
    public string NickName { get; set; } 
    public string PublicId { get; set; }
    public long CurrentRoomId { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime LastActiveAt { get; set; }
    
    private UserSession(){ }
    
    public static UserSession Create(long userId, string email, string nickName, string publicId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id cannot be null or whitespace", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(publicId))
            throw new ArgumentException("Public id cannot be null or whitespace", nameof(publicId));
            
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            Email = email,
            NickName = nickName,
            PublicId = publicId,
            LoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
    }

    public void SetRoomId(long roomId)
    {
        CurrentRoomId = roomId;
    }

    public static UserSession FromRedis(string sessionId, long userId, string email, string userName, string publicId, long currentRoomId,
        DateTime loginAt, DateTime lastActiveAt)
    {
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            Email = email,
            NickName = userName,
            PublicId = publicId,
            CurrentRoomId = currentRoomId,
            LoginAt = loginAt,
            LastActiveAt = lastActiveAt
        };
    }
}