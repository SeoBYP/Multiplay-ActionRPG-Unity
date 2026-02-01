using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.LeaveRoom;

[MemoryPackable]
public partial class LeaveRoomRequest
{
    // TODO: 나중에 JWT 연동 시 제거
    public long UserId { get; set; }
    public long RoomId { get; set; }

    public LeaveRoomRequest(long userId, long roomId)
    {
        UserId = userId;
        RoomId = roomId;
    }
}