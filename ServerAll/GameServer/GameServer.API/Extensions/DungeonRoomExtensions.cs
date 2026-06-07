using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.User.Interfaces;
using GameServer.Domain.Entities;
using GameServer.Grpc.DungeonLobby;
using UserEntity = GameServer.Domain.Entities.User.User;

namespace GameServer.API.Extension;

public static class DungeonRoomExtensions
{
    public static async Task<RoomInfo> ToRoomInfo(
        this DungeonRoom room,
        IUserRepository userRepository,
        IDungeonRoomPlayerRepository dungeonRoomPlayerRepository)
    {
        var info = new RoomInfo
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            HostUserId = room.HostUserId,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToGrpc(),
        };

        var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(room.RoomId);
        var users = await userRepository.GetByIdsAsync(players.Select(player => player.UserId).ToList());
        foreach (var user in users)
        {
            info.CurrentPlayers.Add(user.ToUserInfo());
        }

        return info;
    }

    /// <summary>배치 조회로 미리 모은 플레이어 User 목록으로 RoomInfo를 조립 (N+1 회피, 추가 I/O 없음).</summary>
    public static RoomInfo ToRoomInfo(this DungeonRoom room, IReadOnlyList<UserEntity> playersInRoom)
    {
        var info = new RoomInfo
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            HostUserId = room.HostUserId,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToGrpc(),
        };

        foreach (var user in playersInRoom)
            info.CurrentPlayers.Add(user.ToUserInfo());

        return info;
    }

    public static RoomStatus ToDomain(this RoomStatusType grpcType) => grpcType switch
    {
        RoomStatusType.Waiting => RoomStatus.Waiting,
        RoomStatusType.Starting => RoomStatus.Starting,
        RoomStatusType.Playing => RoomStatus.Playing,
        RoomStatusType.Closed => RoomStatus.Closed,
        _ => throw new ArgumentException()
    };

    public static RoomStatusType ToGrpc(this RoomStatus domainType) => domainType switch
    {
        RoomStatus.Waiting => RoomStatusType.Waiting,
        RoomStatus.Starting => RoomStatusType.Starting,
        RoomStatus.Playing => RoomStatusType.Playing,
        RoomStatus.Closed => RoomStatusType.Closed,
        _ => throw new ArgumentException()
    };
}
