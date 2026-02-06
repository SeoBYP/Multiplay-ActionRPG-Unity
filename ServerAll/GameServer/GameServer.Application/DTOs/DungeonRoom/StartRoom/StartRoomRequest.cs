using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.StartRoom;

[MemoryPackable]
public partial class StartRoomRequest
{
    public long RoomId { get; set; }
    
    public StartRoomRequest(long roomId)
    {
        RoomId = roomId;
    }
}