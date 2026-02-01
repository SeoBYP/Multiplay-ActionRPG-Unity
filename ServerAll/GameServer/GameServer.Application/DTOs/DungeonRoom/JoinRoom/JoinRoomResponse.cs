using GameServer.Application.DTOs.DungeonRoom.Rooms;
using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.JoinRoom;

[MemoryPackable]
public partial class JoinRoomResponse
{
    public RoomInfoDto RoomInfo { get; set; }

    public JoinRoomResponse(RoomInfoDto roomInfo)
    {
        RoomInfo = roomInfo;
    }
}