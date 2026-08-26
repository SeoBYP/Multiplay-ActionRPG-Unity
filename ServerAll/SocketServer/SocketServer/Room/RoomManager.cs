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
                playerInfo.AttackPower, playerInfo.Defense, playerInfo.MaxHealth, playerInfo.MaxMana);
        }

        // 몬스터 초기 스폰(서버 권위) — 맵 경계도 함께 보관(이동 clamp 기준).
        room.SpawnMonsters(layout.Monsters, layout.Bounds, layout.MonsterLevel); // AC-E2: 던전 기본 레벨(0=미저작→L1)

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

    /// <summary>
    /// 같은 UserId 의 옛 세션이 방에 남아 있으면 자리를 비운다(<b>재접속 인수</b>).
    ///
    /// <b>왜</b>: 에디터 Play 정지·강제 종료처럼 FIN 없이 끊기면 서버는 그 세션을 바로 죽었다고 못 본다.
    /// 유휴 타임아웃까지(실측 63초) 세션이 방에 남아 2/2 방은 <b>본인조차</b> "Room is full" 로 거절됐다
    /// — 재접속 유예(60s)는 그 뒤에야 시작되므로 유예가 있어도 돌아올 방법이 없었다.
    ///
    /// <b>graceful: true</b> 로 비운다 — 세션만 교체하고 액터(위치·HP)는 보존해야 원래 자리로 복귀한다.
    /// 자리를 넘겨받는 것은 <b>같은 UserId</b> 뿐이다(남의 자리를 뺏지 않는다).
    /// </summary>
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

    /// <param name="graceful">
    /// true = 크래시/네트워크 끊김(C_PlayerLeave 없음). 방에 다른 플레이어가 남아 있으면
    ///        재접속 유예 창(<see cref="Room.ReconnectGraceMs"/>) 동안 참가자·액터를 보존하고
    ///        S_PlayerLeft 브로드캐스트·association 정리를 <b>보류</b>한다(재접속하면 복귀).
    ///        유예 안에 재접속 안 하면 RoomTickService 의 <see cref="SweepDisconnectedPlayers"/> 가 만료 확정.
    /// false = 명시 퇴장(C_PlayerLeave) — 즉시 퇴장 확정(상태 제거 + 이벤트 발행).
    /// 단, graceful 이라도 방이 비면(마지막 플레이어) 즉시 확정 — 빈 방은 유예 없이 제거된다.
    /// </param>
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

        // 크래시인데 방에 다른 플레이어가 남음 → 재접속 유예. 퇴장 확정(브로드캐스트/association 정리)을 보류한다.
        if (graceful && room.Sessions.Count > 0)
            return true;

        // 명시 퇴장 OR 크래시로 방이 빔 → 즉시 퇴장 확정.
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

    /// <summary>
    /// 재접속 유예가 만료된 끊김 플레이어를 영구 퇴장으로 확정한다(RoomTickService 가 10Hz 로 호출).
    /// 만료 userId: S_PlayerLeft 브로드캐스트 + PlayerLeftRoomMessage 발행(association 정리).
    /// _rooms 에 남은 방은 항상 연결 세션이 ≥1(마지막 세션이 떠나면 LeaveRoom 이 빈 방을 즉시 제거)이라,
    /// 만료 정리 후에도 방은 유지된다.
    /// </summary>
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

    /// <summary>
    /// 퇴장 후처리: 빈 방이면 메모리에서 제거하고, 인증된 유저(UserId>0)면
    /// GameServer에 PlayerLeftRoomMessage를 발행한다(RoomEmptied 포함).
    /// 빈 방 여부와 무관하게 항상 발행해야 GameServer가 해당 유저 association을 정리한다.
    /// </summary>
    private void PublishPlayerLeft(Room room, long roomId, long userId)
    {
        var emptied = room.Sessions.Count == 0;
        if (emptied && _rooms.TryRemove(roomId, out _))
        {
            // 빈 방 제거 — 유예 보존 중이던 다른 끊김 플레이어들도 함께 영구 퇴장 확정(association 정리 누락 방지).
            // (예: 전원 크래시 — 마지막 세션이 떠날 때 앞서 끊긴 플레이어 상태가 유예로 남아 있을 수 있음)
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

    /// <summary>
    /// 던전 클리어(몬스터 전멸)를 GameServer에 발행한다. 현재 방 참가자(UserId)와 MapId를 담는다.
    /// 클라 브로드캐스트(S_DungeonClear)는 호출자(CombatHandler)가 별도로 한다 — 여기선 서버 간 통지만.
    /// 전멸 1회 보장은 Room.TryMarkCleared 책임이므로 호출자가 true 일 때만 부른다.
    /// </summary>
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
