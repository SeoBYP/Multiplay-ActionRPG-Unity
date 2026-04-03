namespace GameServer.Domain.Entities.GameSession;

public enum GameSessionStatus
{
    Active, 
    Ended
}

public class GameSession
{
    public long GameSessionId { get; private set; }
    public long RoomId { get; private set; }
    public string SocketIp { get; private set; }
    public int SocketPort { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public GameSessionStatus Status { get; private set; }
    
    private GameSession() { }

    public static GameSession Create(long roomId, string socketIp, int socketPort)
    {
        if (roomId <= 0) throw new ArgumentException("RoomId invalid");
        if (string.IsNullOrWhiteSpace(socketIp)) throw new ArgumentException("SocketIp invalid");
        if (socketPort <= 0) throw new ArgumentException("SocketPort invalid");

        return new GameSession
        {
            RoomId = roomId,
            SocketIp = socketIp,
            SocketPort = socketPort,
            StartedAt = DateTime.UtcNow,
            Status = GameSessionStatus.Active
        };
    }

    public void End()
    {
        if (Status == GameSessionStatus.Ended)
            throw new InvalidOperationException("Already ended");
        Status = GameSessionStatus.Ended;
        EndedAt = DateTime.UtcNow;
    }
    
    public void SetId(long id) // Repository에서 채번 후 주입
    {
        if (GameSessionId != 0) throw new InvalidOperationException("Id already set");
        GameSessionId = id;
    }

    public static GameSession FromRedis(long gameSessionId, long roomId, string socketIp, int socketPort,
        DateTime startedAt, DateTime? endedAt, GameSessionStatus status)
    {
        return new GameSession
        {
            GameSessionId = gameSessionId,
            RoomId = roomId,
            SocketIp = socketIp,
            SocketPort = socketPort,
            StartedAt = startedAt,
            EndedAt = endedAt,
            Status = status
        };
    }
}