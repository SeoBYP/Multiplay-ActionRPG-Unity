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
            room.AddPlayer(
                playerInfo.UserId, playerInfo.Nickname, playerInfo.SpawnIndex,
                spawn.X, spawn.Y, spawn.Z, spawn.RotY,
                playerInfo.AttackPower, playerInfo.Defense, playerInfo.MaxHealth, playerInfo.MaxMana,
                playerInfo.AbilityIds);
        }

        room.SpawnMonsters(layout.Monsters, layout.Bounds, layout.MonsterLevel); // MonsterLevel 0 = 미저작 → L1

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

        if (room != null)
            EvictStaleSession(room, session);

        if (room != null && room.Sessions.Add(session))
        {
            _playerRooms[session.SessionId] = room.RoomId;
            session.Room = room;
            return true;
        }

        return false;
    }

    // 같은 UserId 의 옛 세션이 방에 남아 있으면 비운다(재접속 인수). FIN 없는 끊김은 유휴 타임아웃까지 안 보여 만원 방이 본인도 거절한다.
    // graceful: true — 액터(위치·HP)를 보존해야 원래 자리로 복귀. 같은 UserId 만 인수한다(남의 자리 불가).
    private void EvictStaleSession(Room room, Session incoming)
    {
        var stale = room.Sessions.FindByUserId(incoming.UserId, incoming.SessionId);
        if (stale is null)
            return;

        _logger.LogInformation(
            "Room {RoomId} takeover — UserId={UserId} old session {OldSessionId} evicted for {NewSessionId}",
            room.RoomId, incoming.UserId, stale.SessionId, incoming.SessionId);

        room.Leave(stale.SessionId, graceful: true);
        _playerRooms.TryRemove(stale.SessionId, out _);
        stale.Room = null;
    }

    // graceful=true(크래시/끊김)이고 방에 다른 플레이어가 남으면 유예 동안 보존하고 S_PlayerLeft·association 정리를 보류.
    // 방이 비면 graceful 이어도 즉시 확정. false(C_PlayerLeave)는 즉시 확정.
    public bool LeaveRoom(Session session, bool graceful = false)
    {
        if (!_playerRooms.TryRemove(session.SessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var userId = session.UserId;
        room.Leave(session.SessionId, graceful);
        session.Room = null;

        if (graceful && room.Sessions.Count > 0)
            return true;

        if (userId > 0)
        {
            room.Sessions.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    // 유예 만료 플레이어 영구 퇴장 확정(10Hz). _rooms 의 방은 항상 세션 ≥1 이라 정리 후에도 방은 유지된다.
    public void SweepDisconnectedPlayers(long nowMs)
    {
        foreach (var room in _rooms.Values)
        {
            var expired = room.SweepExpiredDisconnected(nowMs, Room.ReconnectGraceMs);
            if (expired.Count == 0)
                continue;

            foreach (var userId in expired)
            {
                _logger.LogInformation(
                    "Room {RoomId} reconnect grace expired for User {UserId} — finalizing leave",
                    room.RoomId, userId);

                if (userId > 0)
                    room.Sessions.Broadcast(new S_PlayerLeft { UserId = userId });

                PublishPlayerLeft(room, room.RoomId, userId);
            }
        }
    }

    public bool LeaveRoom(ulong sessionId)
    {
        if (!_playerRooms.TryRemove(sessionId, out long roomId))
            return false;

        var room = _rooms.GetValueOrDefault(roomId);
        if (room == null)
            return false;

        var session = room.Sessions.Get(sessionId);
        var userId = session?.UserId ?? 0;
        room.Leave(sessionId);
        if (userId > 0)
        {
            room.Sessions.Broadcast(new S_PlayerLeft
            {
                UserId = userId
            });
        }

        PublishPlayerLeft(room, roomId, userId);
        return true;
    }

    // 빈 방이면 제거. 빈 방 여부와 무관하게 항상 발행해야 GameServer 가 유저 association 을 정리한다.
    private void PublishPlayerLeft(Room room, long roomId, long userId)
    {
        var emptied = room.Sessions.Count == 0;
        if (emptied && _rooms.TryRemove(roomId, out _))
        {
            // 전원 크래시 시 앞서 끊겨 유예 중이던 플레이어도 함께 확정(association 정리 누락 방지).
            foreach (var member in room.Actors.Members())
            {
                if (member.UserId > 0 && member.UserId != userId && member.DisconnectedAtMs is not null)
                {
                    _ = _lifecycleQueue.EnqueueAsync(new PlayerLeftRoomMessage
                    {
                        RoomId = roomId,
                        UserId = member.UserId,
                        RoomEmptied = true
                    });
                }
            }

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

    // 서버 간 통지만(클라 S_DungeonClear 는 CombatHandler). 호출자는 TryMarkCleared 가 true 일 때만 부른다.
    public void PublishDungeonClear(Room room)
    {
        var participants = room.Actors.Members().Select(m => m.UserId).ToArray();
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

    // PickupId="{RoomId}:{GroundId}" 가 멱등 키(GameServer 가 SET claim 으로 1회만 지급).
    // 호출자는 TryPickup 성공(non-null) 시에만 부른다.
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
        return _rooms.Values.FirstOrDefault(r => !r.Sessions.IsFull);
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
