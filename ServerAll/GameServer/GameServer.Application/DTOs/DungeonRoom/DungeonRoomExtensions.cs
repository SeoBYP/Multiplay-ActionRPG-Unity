using GameServer.Application.DTOs.DungeonRoom.CreateRoom;
using GameServer.Application.DTOs.DungeonRoom.Rooms;

namespace GameServer.Application.DTOs.DungeonRoom;

public static class DungeonRoomExtensions
{
    public static CreateRoomResponse ToCreateRoomResponse(this Domain.Entities.DungeonRoom room)
    {
        var result = room.ToRoomInfoDto();
        return new CreateRoomResponse
        (
            result,
            room.CreatedAt
        );
    }

    public static RoomInfoDto ToRoomInfoDto(this Domain.Entities.DungeonRoom dungeonRoom)
    {
        return new RoomInfoDto(
            dungeonRoom.RoomId,
            dungeonRoom.RoomName,
            dungeonRoom.HostUserId,
            dungeonRoom.GetPlayerCount(),
            dungeonRoom.MaxPlayers,
            dungeonRoom.Status.ToString()
            );
    }
    
    public static GetRoomsResponse ToGetRoomsResponse(this IEnumerable<Domain.Entities.DungeonRoom> rooms)
    {
        var roomList = rooms.ToList();
        return new GetRoomsResponse
        {
            Rooms = roomList.Select(r => r.ToRoomInfoDto()).ToList(),
            TotalCount = roomList.Count
        };
    }
}