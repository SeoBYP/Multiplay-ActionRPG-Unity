namespace GameServer.Domain.Entities.GameSession;

public class GameSessionPlayer
{
    public long GameSessionId { get; private set; }
    public long UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    
    private GameSessionPlayer() { }

    public static GameSessionPlayer Create(long gameSessionId, long userId)
    {
        if (gameSessionId <= 0) throw new ArgumentException("GameSessionId invalid");
        if (userId <= 0) throw new ArgumentException("UserId invalid");

        return new GameSessionPlayer
        {
            GameSessionId = gameSessionId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
    }

    public static GameSessionPlayer FromRedis(long gameSessionId, long userId, DateTime joinedAt)
    {
        return new GameSessionPlayer
        {
            GameSessionId = gameSessionId,
            UserId = userId,
            JoinedAt = joinedAt
        };
    }
}
