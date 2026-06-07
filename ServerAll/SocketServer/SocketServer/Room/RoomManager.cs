using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Server.Loot;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Room;

public class RoomManager
{
    private readonly ConcurrentDictionary<long, Room> _rooms = new();
    private readonly ConcurrentDictionary<ulong, long> _playerRooms = new();
    private readonly ConcurrentDictionary<long, long> _userRoomIndex = new();
    private readonly ILogger<RoomManager> _logger;
    private readonly ILogger<Room> _roomLogger;
    private readonly IRoomLifecyclePublisher _lifecycleQueue;
    private readonly IDungeonResultPublisher _dungeonResultQueue;
    private readonly ILootPickupPublisher _lootPickupQueue;

    public RoomManager(
        ILogger<RoomManager> logger,
        ILogger<Room> roomLogger,
        IRoomLifecyclePublisher lifecycleQueue,
        IDungeonResultPublisher dungeonResultQueue,
        ILootPickupPublisher lootPickupQueue)
    {
        _logger = logger;
        _roomLogger = roomLogger;
        _lifecycleQueue = lifecycleQueue;
        _dungeonResultQueue = dungeonResultQueue;
        _lootPickupQueue = lootPickupQueue;
    }

    private readonly ConcurrentDictionary<long, GameStartRequestedMessage> _roomMessages = new();

    public Room? CreateRoom(long msgRoomId, IReadOnlyList<PlayerInfo> msgPlayerIds, GameStartRequestedMessage message)
    {
        var room = new Room(msgRoomId, msgPlayerIds, _roomLogger);

        if (!_rooms.TryAdd(msgRoomId, room))
        {
            _logger.LogWarning("Failed to create room {RoomId}", msgRoomId);
            return null;
        }

        _roomMessages[msgRoomId] = message;
        room.MapId = message.MapId;
        var layout = SpawnLayoutTable.Get(message.MapId);
        foreach (var playerInfo in message.PlayerInfos)
        {
            _userRoomIndex[playerInfo.UserId] = msgRoomId;
            var spawn = SpawnResolver.Resolve(layout, playerInfo.SpawnIndex);
            room.InitPlayerState(
                playerInfo.UserId, playerInfo.Nickname, playerInfo.SpawnIndex,
                spawn.X, spawn.Y, spawn.Z, spawn.RotY);
        }

        // 몬스터 초기 스폰(서버 권위) — 맵 경계도 함께 보관(이동 clamp 기준).
        room.SpawnMonsters(layout.Monsters, layout.Bounds);

        _logger.LogInformation("Room {RoomId} created with {MaxPlayers} players", msgRoomId, msgPlayerIds.Count);
        return room;
    }

    public GameStartRequestedMessage? GetRoomMessage(long roomId)
    {
        return _roomMessages.GetValueOrDefault(roomId);
    }

    public bool JoinRoom(Session session, long? roomId = null)
    {
        LeaveRoom(session.SessionId);

        Room? room;
        if (roomId.HasValue)
        {
            room = _rooms.GetValueOrDefault(roomId.Value);
            if (room == null)
            {
                _logger.LogWarning("Room {RoomId} not found", roomId.Value);
                return false;
            }
        }
        else
        {
            room = FindAvailableRoom();
        }

        if (room != null && room.Join(session))
        {
            _playerRooms[session.SessionId] = room.RoomId;
            session.Room = room;
            return true;
        }

        return false;
    }

    public bool LeaveRoom(Session session)
    {
        if (!_playerRooms.TryRemove(session.SessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var userId = session.UserId;
        room.Leave(session.SessionId);
        session.Room = null;
        if (userId > 0)
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    public bool LeaveRoom(ulong sessionId)
    {
        if (!_playerRooms.TryRemove(sessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var session = room.GetSession(sessionId);
        var userId = session?.UserId ?? 0;
        room.Leave(sessionId);
        if (userId > 0)
        {
            room.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    /// <summary>
    /// 퇴장 후처리: 빈 방이면 메모리에서 제거하고, 인증된 유저(UserId>0)면
    /// GameServer에 PlayerLeftRoomMessage를 발행한다(RoomEmptied 포함).
    /// 빈 방 여부와 무관하게 항상 발행해야 GameServer가 해당 유저 association을 정리한다.
    /// </summary>
    private void PublishPlayerLeft(Room room, long roomId, long userId)
    {
        var emptied = room.MemberCount == 0;
        if (emptied && _rooms.TryRemove(roomId, out _))
        {
            RemoveUserRoomIndexes(roomId);
            _roomMessages.TryRemove(roomId, out _);
            _logger.LogInformation("Room {RoomId} removed because it is empty", roomId);
        }

        if (userId > 0)
        {
            _ = _lifecycleQueue.EnqueueAsync(new PlayerLeftRoomMessage
            {
                RoomId = roomId,
                UserId = userId,
                RoomEmptied = emptied
            });
        }
    }

    /// <summary>
    /// 던전 클리어(몬스터 전멸)를 GameServer에 발행한다. 현재 방 참가자(UserId)와 MapId를 담는다.
    /// 클라 브로드캐스트(S_DungeonClear)는 호출자(CombatHandler)가 별도로 한다 — 여기선 서버 간 통지만.
    /// 전멸 1회 보장은 Room.TryMarkCleared 책임이므로 호출자가 true 일 때만 부른다.
    /// </summary>
    public void PublishDungeonClear(Room room)
    {
        var participants = room.GetAllPlayerStates().Select(p => p.UserId).ToArray();
        _ = _dungeonResultQueue.EnqueueAsync(new DungeonClearMessage
        {
            RoomId = room.RoomId,
            MapId = room.MapId,
            Participants = participants,
        });
        _logger.LogInformation(
            "Room {RoomId} cleared (MapId={MapId}, participants={Count})",
            room.RoomId, room.MapId, participants.Length);
    }

    /// <summary>
    /// 줍기 확정을 GameServer 에 발행한다(인벤토리 영속 지급). 줍기 1회 보장은 Room.TryPickup(경쟁 중재)
    /// 책임이므로 호출자가 성공(non-null GroundItem)일 때만 부른다.
    /// PickupId = "{RoomId}:{GroundId}" — GroundId 는 방 내 고유·픽업당 1회 제거라 멱등 키로 충분
    /// (GameServer 가 Redis SET claim 으로 중복 메시지에도 1회만 지급).
    /// 클라 브로드캐스트(S_GroundItemRemoved)·토스트(S_ItemPickedUp)는 호출자(LootHandler)가 별도로 한다.
    /// </summary>
    public void PublishItemPickup(Room room, long userId, GroundItem item)
    {
        string pickupId = $"{room.RoomId}:{item.GroundId}";
        _ = _lootPickupQueue.EnqueueAsync(new ItemPickedUpMessage
        {
            UserId = userId,
            ItemId = item.ItemId,
            Qty = item.Qty,
            PickupId = pickupId,
        });
        _logger.LogInformation(
            "Room {RoomId} pickup: UserId={UserId} ItemId={ItemId} Qty={Qty} PickupId={PickupId}",
            room.RoomId, userId, item.ItemId, item.Qty, pickupId);
    }

    public Room? GetPlayerRoom(ulong sessionId)
    {
        if (_playerRooms.TryGetValue(sessionId, out long roomId))
        {
            return _rooms.GetValueOrDefault(roomId);
        }

        return null;
    }

    public Room? FindAvailableRoom()
    {
        return _rooms.Values.FirstOrDefault(r => !r.IsFull);
    }

    public Room? GetRoom(long roomId)
    {
        return _rooms.GetValueOrDefault(roomId);
    }

    public Room? GetAssignedRoom(long userId)
    {
        if (!_userRoomIndex.TryGetValue(userId, out var roomId))
        {
            return null;
        }

        return _rooms.GetValueOrDefault(roomId);
    }

    public List<Room> GetAllRooms()
    {
        return _rooms.Values.ToList();
    }

    private void RemoveUserRoomIndexes(long roomId)
    {
        if (!_roomMessages.TryGetValue(roomId, out var message))
        {
            return;
        }

        foreach (var playerInfo in message.PlayerInfos)
        {
            _userRoomIndex.TryRemove(playerInfo.UserId, out _);
        }
    }

    public int RoomCount => _rooms.Count;
}
