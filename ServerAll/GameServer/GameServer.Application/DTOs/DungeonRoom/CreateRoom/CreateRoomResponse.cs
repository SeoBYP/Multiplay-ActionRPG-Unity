using GameServer.Application.DTOs.DungeonRoom.Rooms;
using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.CreateRoom;

[MemoryPackable]
public partial class CreateRoomResponse(RoomInfoDto roomInfo, DateTime createdAt)
{
    public RoomInfoDto RoomInfo { get; set; } = roomInfo;
    public DateTime CreatedAt { get; set; } = createdAt;
}