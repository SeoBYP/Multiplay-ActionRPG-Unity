using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;
using GameServer.Infrastructure.Interfaces.User;
using GameServer.Infrastructure.Repositories.User;

namespace GameServer.API.Extension;

public static class DungeonRoomExtensions
{
    public static async Task<RoomInfo> ToRoomInfo(this DungeonRoom room, IUserRepository userRepository)
    {
        var info = new RoomInfo
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            HostUserId = room.HostUserId,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToGrpc(),
        };
        foreach (var currentPlayerId in room.CurrentPlayers)
        {
            var currentPlayer = await userRepository.GetByIdAsync(currentPlayerId);
            if (currentPlayer is null) 
                continue;
            info.CurrentPlayers.Add(currentPlayer.ToUserInfo());
        }

        return info;
    }

    public static RoomStatus ToDomain(this RoomStatusType grpcType) => grpcType switch
    {
        RoomStatusType.Waiting => RoomStatus.Waiting,
        RoomStatusType.Playing => RoomStatus.Playing,
        RoomStatusType.Closed => RoomStatus.Closed,
        _ => throw new ArgumentException()
    };

    public static RoomStatusType ToGrpc(this RoomStatus domainType) => domainType switch
    {
        RoomStatus.Waiting => RoomStatusType.Waiting,
        RoomStatus.Playing => RoomStatusType.Playing,
        RoomStatus.Closed => RoomStatusType.Closed,
        _ => throw new ArgumentException()
    };
}