namespace GameServer.Domain.Entities;

public class UserSession
{
    public string SessionId { get; set; }
    public long UserId { get; set; }
    public string Email { get; set; } 
    public string NickName { get; set; } 
    public string PublicId { get; set; }
    public long CurrentRoomId { get; private set; }
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
            CurrentRoomId = 0,
            LoginAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow
        };
    }

    public static UserSession FromRedis(string sessionId, long userId, string email, string nickName, string publicId,
        DateTime loginAt, DateTime lastActiveAt, long currentRoomId = 0)
    {
        return new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            Email = email,
            NickName = nickName,
            PublicId = publicId,
            CurrentRoomId = currentRoomId,
            LoginAt = loginAt,
            LastActiveAt = lastActiveAt
        };
    }

    public void SetRoomId(long roomId)
    {
        if (roomId < 0)
            throw new ArgumentException("RoomId cannot be negative", nameof(roomId));

        CurrentRoomId = roomId;
    }
}
