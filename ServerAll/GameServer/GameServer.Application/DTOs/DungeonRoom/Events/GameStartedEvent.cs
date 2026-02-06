using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Events;

[MemoryPackable]
public partial class GameStartedEvent
{
    public RoomInfoDto RoomInfo { get; set; }
    
    public GameStartedEvent(RoomInfoDto roomInfo)
    {
        RoomInfo = roomInfo;
    }
}