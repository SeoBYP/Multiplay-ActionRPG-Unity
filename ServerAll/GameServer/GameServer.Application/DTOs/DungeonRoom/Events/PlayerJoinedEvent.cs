using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Events;

[MemoryPackable]
public partial class PlayerJoinedEvent
{
    public long JoinUserId { get; set; }
    public string JoinUserName { get; set; } = "";
    public RoomInfoDto RoomInfo { get; set; }

    public PlayerJoinedEvent(long joinUserId, string joinUserName, RoomInfoDto roomInfo)
    {
        JoinUserId = joinUserId;
        JoinUserName = joinUserName;
        RoomInfo = roomInfo;
    }
}