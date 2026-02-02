using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.UpdateRoom;

[MemoryPackable]
public partial class UpdateRoomResponse
{
    public bool IsSuccess { get; set; }
    public RoomInfoDto RoomInfo { get; set; }
    
    public UpdateRoomResponse(bool isSuccess, RoomInfoDto roomInfo)
    {
        IsSuccess = isSuccess;
        RoomInfo = roomInfo;
    }
}