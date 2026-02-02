using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom;

[MemoryPackable]
public partial class RoomInfoDto
{
    public long RoomId { get; set; }
    public string RoomName { get; set; } = "";
    public long HostUserId { get; set; }
    public int CurrentPlayers { get; set; }
    public int MaxPlayers { get; set; }
    public string Status { get; set; } = "";

    public RoomInfoDto(long roomId, string roomName, long hostUserId, int currentPlayers, int maxPlayers, string status)
    {
        RoomId = roomId;
        RoomName = roomName;
        HostUserId = hostUserId;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        Status = status;
    }
}