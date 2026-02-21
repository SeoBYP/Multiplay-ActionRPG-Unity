using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;

namespace GameServer.API.Extension;

public static class DungeonRoomExtensions
{
    public static RoomInfo ToRoomInfo(this DungeonRoom room)
    {
        var info = new RoomInfo
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            HostUserId = room.HostUserId,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToString(),
        };
        foreach (var currentPlayerId in room.CurrentPlayers)
        {
            info.CurrentPlayers.Add(currentPlayerId);
        }
        return info;
    }
}