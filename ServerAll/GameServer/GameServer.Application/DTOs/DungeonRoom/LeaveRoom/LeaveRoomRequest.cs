using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.LeaveRoom;

[MemoryPackable]
public partial class LeaveRoomRequest
{
    public long RoomId { get; set; }

    public LeaveRoomRequest(long roomId)
    {
        RoomId = roomId;
    }
}