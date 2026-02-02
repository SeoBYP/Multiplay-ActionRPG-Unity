using GameServer.Application.DTOs.DungeonRoom.CreateRoom;
using GameServer.Application.DTOs.DungeonRoom.Rooms;

namespace GameServer.Application.DTOs.DungeonRoom;

public static class DungeonRoomExtensions
{
    extension(Domain.Entities.DungeonRoom room)
    {
        public CreateRoomResponse ToCreateRoomResponse()
        {
            var result = room.ToRoomInfoDto();
            return new CreateRoomResponse
            (
                result,
                room.CreatedAt
            );
        }

        public RoomInfoDto ToRoomInfoDto()
        {
            return new RoomInfoDto(
                room.RoomId,
                room.RoomName,
                room.HostUserId,
                room.GetPlayerCount(),
                room.MaxPlayers,
                room.Status.ToString()
            );
        }

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