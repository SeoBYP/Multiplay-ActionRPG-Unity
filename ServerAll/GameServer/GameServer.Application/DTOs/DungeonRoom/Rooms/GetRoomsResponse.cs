using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Rooms;

[MemoryPackable]
public partial class GetRoomsResponse
{
    public List<RoomInfoDto> Rooms { get; set; } = new();
    public int TotalCount { get; set; }
}