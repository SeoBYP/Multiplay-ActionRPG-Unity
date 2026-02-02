using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Room;

[MemoryPackable]
public partial class GetRoomRequest
{
    public long RoomId { get; set; }
    
    public GetRoomRequest(long roomId)
    {
        RoomId = roomId;
    }
}