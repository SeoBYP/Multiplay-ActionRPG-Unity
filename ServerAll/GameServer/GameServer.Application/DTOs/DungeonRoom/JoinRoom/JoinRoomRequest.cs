using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.JoinRoom;

[MemoryPackable]
public partial class JoinRoomRequest
{
    // TODO: 나중에 JWT 연동 시 제거
    public long RoomId { get; set; }
    
    public JoinRoomRequest(long roomId)
    {
        RoomId = roomId;
    }
}