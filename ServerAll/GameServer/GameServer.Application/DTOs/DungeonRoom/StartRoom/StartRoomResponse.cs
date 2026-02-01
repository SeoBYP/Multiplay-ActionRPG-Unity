using GameServer.Application.DTOs.DungeonRoom.Rooms;
using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.StartRoom;

[MemoryPackable]
public partial class StartRoomResponse
{
    public RoomInfoDto RoomInfo { get; set; }

    public StartRoomResponse(RoomInfoDto roomInfo)
    {
        RoomInfo = roomInfo;
    }
}