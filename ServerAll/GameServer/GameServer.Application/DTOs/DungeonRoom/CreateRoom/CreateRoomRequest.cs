using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.CreateRoom;

[MemoryPackable]
public partial class CreateRoomRequest
{
    public long UserId { get; set; }
    public string RoomName { get; set; } = "";
    public int MaxPlayers { get; set; } = 4;
    
    public CreateRoomRequest(long userId, string roomName, int maxPlayers)
    {
        UserId = userId;
        RoomName = roomName;
        MaxPlayers = maxPlayers;
    }
}