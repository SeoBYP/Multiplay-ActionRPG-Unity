using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Room;

[MemoryPackable]
public partial class GetRoomResponse
{
    public RoomInfoDto RoomInfo { get; set; }
    
    public GetRoomResponse(RoomInfoDto roomInfo)
    {
        RoomInfo = roomInfo;
    }
}