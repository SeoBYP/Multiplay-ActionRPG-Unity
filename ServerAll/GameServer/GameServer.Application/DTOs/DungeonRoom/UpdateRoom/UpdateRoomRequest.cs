using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.UpdateRoom;

[MemoryPackable]
public partial class UpdateRoomRequest
{
    public long UserId { get; set; }
    public long RoomId { get; set; }
    
    public string? RoomName { get; set; }
    public int? MaxPlayers { get; set; }
}