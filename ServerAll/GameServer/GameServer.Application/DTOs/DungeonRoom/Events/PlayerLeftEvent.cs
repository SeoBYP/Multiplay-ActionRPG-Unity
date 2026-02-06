using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Events;

[MemoryPackable]
public partial class PlayerLeftEvent
{
    public long LeaveUserId { get; set; }
    public string LeaveUserName { get; set; } = "";
    public RoomInfoDto RoomInfo { get; set; }

    public PlayerLeftEvent(long leaveUserId, string leaveUserName, RoomInfoDto roomInfo)
    {
        LeaveUserId = leaveUserId;
        LeaveUserName = leaveUserName;
        RoomInfo = roomInfo;
    }
}