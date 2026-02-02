using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.LeaveRoom;

[MemoryPackable]
public partial class LeaveRoomResponse
{
    public bool IsSuccess { get; set; }
    
    public LeaveRoomResponse(bool isSuccess)
    {
        IsSuccess = isSuccess;
    }
}