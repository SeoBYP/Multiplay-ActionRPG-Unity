namespace GameServer.Domain.Entities;

public enum RoomStatus
{
    Waiting,
    Starting,
    Playing,
    Closed
}

public class DungeonRoom
{
    public long RoomId { get; private set; }
    public string RoomName { get; private set; } = string.Empty;
    public long HostUserId { get; private set; }
    public int MaxPlayers { get; private set; }
    public RoomStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private DungeonRoom()
    {
    }

    public DungeonRoom Clone()
        => FromRedis(RoomId, RoomName, HostUserId, MaxPlayers, Status, CreatedAt);

    public static DungeonRoom Create(string roomName, long hostUserId, int maxPlayers)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            throw new ArgumentException("Room name cannot be null or whitespace", nameof(roomName));

        if (hostUserId <= 0)
            throw new ArgumentException("Host ID cannot be less than or equal to zero", nameof(hostUserId));

        if (maxPlayers < 2)
            throw new ArgumentException("Max players cannot be less than 2", nameof(maxPlayers));

        return new DungeonRoom
        {
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = RoomStatus.Waiting,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static DungeonRoom FromRedis(
        long roomId,
        string roomName,
        long hostUserId,
        int maxPlayers,
        RoomStatus status,
        DateTime createdAt)
    {
        return new DungeonRoom
        {
            RoomId = roomId,
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = status,
            CreatedAt = createdAt
        };
    }

    public static DungeonRoom FromRoomInfoDto(
        long roomId,
        string roomName,
        long hostUserId,
        int maxPlayers,
        RoomStatus status)
        => new()
        {
            RoomId = roomId,
            RoomName = roomName,
            HostUserId = hostUserId,
            MaxPlayers = maxPlayers,
            Status = status
        };
    
    public bool IsHost(long userId) => HostUserId == userId;

    public void UpdateRoomSettings(
        long userId,
        int currentPlayerCount,
        string? roomName = null,
        int? maxPlayers = null)
    {
        if (!IsHost(userId))
            throw new UnauthorizedAccessException("Only the host can update room settings");

        if (Status == RoomStatus.Playing)
            throw new InvalidOperationException("Cannot update room settings while playing");

        if (roomName is not null)
        {
            if (string.IsNullOrWhiteSpace(roomName))
                throw new ArgumentException("Room name cannot be empty", nameof(roomName));

            RoomName = roomName;
        }

        if (maxPlayers.HasValue)
        {
            if (maxPlayers.Value < 2)
                throw new ArgumentException("Max players cannot be less than 2", nameof(maxPlayers));

            if (maxPlayers.Value < currentPlayerCount)
                throw new InvalidOperationException(
                    $"Cannot reduce max players to {maxPlayers.Value}. Current players: {currentPlayerCount}");

            MaxPlayers = maxPlayers.Value;
        }
    }

    public void StartGame(long userId, int playerCount)
    {
        if (!IsHost(userId))
            throw new UnauthorizedAccessException("User is not host");

        if (playerCount < 2)
            throw new InvalidOperationException("need at least 2 players to start the game");

        if (Status == RoomStatus.Closed)
            throw new InvalidOperationException("Room is already closed");

        if (Status == RoomStatus.Starting || Status == RoomStatus.Playing)
            throw new InvalidOperationException("Room is already starting");

        Status = RoomStatus.Starting;
    }

    public void ChangeHost(long newHostUserId)
    {
        if (newHostUserId <= 0)
            throw new ArgumentException("Host ID cannot be less than or equal to zero", nameof(newHostUserId));

        HostUserId = newHostUserId;
    }

    public void Close()
    {
        Status = RoomStatus.Closed;
    }

    public void MarkGameSessionReady()
    {
        if (Status != RoomStatus.Starting)
            throw new InvalidOperationException("Room is not starting");

        Status = RoomStatus.Playing;
    }
}
