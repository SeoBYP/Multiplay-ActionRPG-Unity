using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Rooms;

[MemoryPackable]
public partial class GetRoomsResponse
{
    public List<DungeonRoom.RoomInfoDto> Rooms { get; set; } = new();
    public int TotalCount { get; set; }
}