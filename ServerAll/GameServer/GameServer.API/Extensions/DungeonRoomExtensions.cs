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
        IDungeonRoomPlayerRepository dungeonRoomPlayerRepository,
        IRoomReadyStore roomReadyStore)
    {
        var players = await dungeonRoomPlayerRepository.GetPlayersByRoomIdAsync(room.RoomId);
        var users = await userRepository.GetByIdsAsync(players.Select(player => player.UserId).ToList());
        var readyUserIds = await roomReadyStore.GetReadyUserIdsAsync(room.RoomId);

        // 입장 순서로 고정한다. 저장소 반환 순서를 그대로 쓰면 호출마다 슬롯이 뒤바뀐다.
        var joinOrder = players
            .OrderBy(player => player.JoinedAt)
            .ThenBy(player => player.UserId)
            .Select((player, index) => (player.UserId, index))
            .ToDictionary(x => x.UserId, x => x.index);

        var orderedUsers = users
            .OrderBy(user => joinOrder.TryGetValue(user.UserId, out var index) ? index : int.MaxValue)
            .ToList();

        return room.BuildRoomInfo(orderedUsers, readyUserIds);
    }

    /// <summary>
    /// 배치 조회로 미리 모은 플레이어 User 목록으로 RoomInfo를 조립 (N+1 회피, 추가 I/O 없음).
    /// 방 <b>목록</b> 화면용.
    /// </summary>
    /// <param name="readyUserIds">
    /// 이 방의 준비 완료 userId. 목록에서 고른 방이 그대로 대기실 State 로 들어가므로
    /// 여기서 비우면 대기실이 열린 직후 전원 미준비로 잘못 그려진다 — 반드시 채워 넘긴다.
    /// </param>
    public static RoomInfo ToRoomInfo(
        this DungeonRoom room,
        IReadOnlyList<UserEntity> playersInRoom,
        IReadOnlySet<long>? readyUserIds = null)
        => room.BuildRoomInfo(playersInRoom, readyUserIds);

    private static RoomInfo BuildRoomInfo(
        this DungeonRoom room,
        IReadOnlyList<UserEntity> playersInRoom,
        IReadOnlySet<long>? readyUserIds)
    {
        var info = new RoomInfo
        {
            RoomId = room.RoomId,
            RoomName = room.RoomName,
            HostUserId = room.HostUserId,
            MaxPlayers = room.MaxPlayers,
            Status = room.Status.ToGrpc(),
            MapId = room.MapId,
        };

        foreach (var user in playersInRoom)
        {
            info.CurrentPlayers.Add(user.ToUserInfo());

            if (room.IsHost(user.UserId))
                info.HostPublicId = user.PublicId;

            // 호스트는 준비 목록에 담기지 않는다(준비 개념 없음 = 항상 준비된 것으로 본다).
            if (readyUserIds is not null && !room.IsHost(user.UserId) && readyUserIds.Contains(user.UserId))
                info.ReadyPublicIds.Add(user.PublicId);
        }

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
