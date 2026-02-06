using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.UpdateRoom;

[MemoryPackable]
public partial class UpdateRoomRequest
{
    public long RoomId { get; set; }
    
    public string? RoomName { get; set; }
    public int? MaxPlayers { get; set; }

    public UpdateRoomRequest(long roomId, string? roomName, int? maxPlayers)
    {
        RoomId = roomId;
        RoomName = roomName;
        MaxPlayers = maxPlayers;
    }
}