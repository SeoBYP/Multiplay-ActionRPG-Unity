using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.Events;

[MemoryPackable]
public partial class CreateRoomEvent
{
    public long CreateUserId { get; set; }
    public string CreateUserName { get; set; } = "";
    public RoomInfoDto RoomInfo { get; set; }
    public DateTime CreatedAt { get; set; }


    public CreateRoomEvent(long createUserId, string createUserName, RoomInfoDto roomInfo, DateTime createdAt)
    {
        CreateUserId = createUserId;
        CreateUserName = createUserName;
        RoomInfo = roomInfo;
        CreatedAt = createdAt;
    }
}