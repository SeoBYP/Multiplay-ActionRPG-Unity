namespace GameServer.Domain.Entities;

public class DungeonRoomPlayer
{
    public long RoomId { get; private set; }
    public long UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private DungeonRoomPlayer()
    {
    }
    
    public static DungeonRoomPlayer Create(long roomId, long userId)
    {
        if (roomId <= 0)
            throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));

        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero", nameof(userId));

        return new DungeonRoomPlayer
        {
            RoomId = roomId,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
    }
    
    public static DungeonRoomPlayer Restore(long roomId, long userId, DateTime joinedAt)
    {
        if (roomId <= 0)
            throw new ArgumentException("RoomId must be greater than zero", nameof(roomId));

        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero", nameof(userId));

        return new DungeonRoomPlayer
        {
            RoomId = roomId,
            UserId = userId,
            JoinedAt = joinedAt
        };
    }
}