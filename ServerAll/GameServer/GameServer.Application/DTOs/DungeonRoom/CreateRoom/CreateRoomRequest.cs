using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.CreateRoom;

[MemoryPackable]
public partial class CreateRoomRequest
{
    public string RoomName { get; set; } = "";
    public int MaxPlayers { get; set; }
    
    public CreateRoomRequest(string roomName, int maxPlayers)
    {
        RoomName = roomName;
        MaxPlayers = maxPlayers;
    }
}