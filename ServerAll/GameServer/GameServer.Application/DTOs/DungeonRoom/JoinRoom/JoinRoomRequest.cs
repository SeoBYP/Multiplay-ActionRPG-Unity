using MemoryPack;

namespace GameServer.Application.DTOs.DungeonRoom.JoinRoom;

[MemoryPackable]
public partial class JoinRoomRequest
{
    // TODO: 나중에 JWT 연동 시 제거
    public long UserId { get; set; }
    public long RoomId { get; set; }
}