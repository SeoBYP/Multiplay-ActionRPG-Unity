using System.Globalization;
using GameServer.Application.Domains.DungeonLobby;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Domain.Entities;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Domains.DungeonRoom;

/// <summary>
/// Redis 기반 던전 방 저장소 구현체
/// </summary>
public class DungeonRoomRepository(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<DungeonRoomRepository> logger) : IDungeonRoomRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string RoomKey = "game:room";
    private const string ActiveRoomsKey = "game:room:active";
    private const string RoomCounterKey = "game:room:id:counter";

    /// <summary>
    /// 새로운 던전 방을 생성하고 Redis에 저장합니다.
    /// </summary>
    /// <param name="hostId">방을 생성하는 방장의 사용자 ID</param>
    /// <param name="roomName">생성할 방의 이름</param>
    /// <param name="maxPlayers">방의 최대 수용 인원 (기본값: 4)</param>
    /// <returns>생성된 DungeonRoom 객체</returns>
    public async Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName,  int maxPlayers = 4, CancellationToken ct = default)
    {
        try
        {
            // 1. 도메인 모델 생성
            var room = Domain.Entities.DungeonRoom.Create(roomName, hostId,maxPlayers);

            // 2. Redis INCR로 RoomId 생성
            var roomId = await _database.StringIncrementAsync(RoomCounterKey);
            room.SetRoomId(roomId);

            // 3. Transaction 시작
            var transaction = _database.CreateTransaction();

            // 4. 방 기본 정보 저장 (Hash)
            _ = transaction.HashSetAsync($"{RoomKey}:{roomId}",
            [
                new HashEntry("RoomId", roomId),
                new HashEntry("RoomName", roomName),
                new HashEntry("HostUserId", hostId),
                new HashEntry("MaxPlayers", room.MaxPlayers),
                new HashEntry("Status", room.Status.ToString()), // Enum → String
                new HashEntry("CreatedAt", room.CreatedAt.ToString("O")), // ISO 8601
            ]);
            
            // 6. 활성 방 목록에 추가
            _ = transaction.SetAddAsync(ActiveRoomsKey, roomId);

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create room");
            return room;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to create dungeon room for host {HostUserId}", hostId);
            throw;
        }
    }

    /// <summary>
    /// 방 고유 ID를 사용하여 던전 방 정보를 상세 조회합니다. (플레이어 목록 포함)
    /// </summary>
    /// <param name="roomId">조회할 방 ID</param>
    /// <returns>DungeonRoom 객체, 존재하지 않는 경우 null</returns>
    public async Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var entries = await _database.HashGetAllAsync($"{RoomKey}:{roomId}");

            if (entries.Length == 0)
                return null;

            return ParseDungeonRoomFromRedis(roomId, entries);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get dungeon room {RoomId}", roomId);
            throw;
        }
    }

    /// <summary>
    /// 사용자 ID를 사용하여 해당 사용자가 현재 참여 중인 던전 방 정보를 조회합니다.
    /// </summary>
    /// <param name="userId">참여 중인 사용자 ID</param>
    /// <returns>DungeonRoom 객체, 참여 중인 방이 없는 경우 null</returns>
    public async Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default)
    {
        return null;
    }

    /// <summary>
    /// 현재 서버에 존재하는 모든 활성 던전 방 목록을 조회합니다.
    /// </summary>
    /// <returns>활성 던전 방 객체 리스트</returns>
    public async Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. 활성 Room에서 모든 RoomId를 조회
            var roomIds = await _database.SetMembersAsync(ActiveRoomsKey);
           
            if (roomIds.Length == 0)
                return Enumerable.Empty<Domain.Entities.DungeonRoom>();
            
            // 2. Batch를 통해서 Redis 요청을 병렬 처리
            var batch = _database.CreateBatch();

            var roomTasks = roomIds
                .Select(roomId => batch.HashGetAllAsync($"{RoomKey}:{roomId}"))
                .ToList();
            
            batch.Execute();

            // 3. 결과 파싱
            var rooms = new List<Domain.Entities.DungeonRoom>();
            for (int i = 0; i < roomIds.Length; i++)
            {
                var entries = await roomTasks[i];
                if (entries.Length == 0)
                    continue;
            
                var room = ParseDungeonRoomFromRedis(
                    long.Parse(roomIds[i].ToString()), 
                    entries);
            
                if (room is not null)
                    rooms.Add(room);
            }

            return rooms;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get all active dungeon rooms");
            throw;
        }
    }

    /// <summary>
    /// 현재 활성화된 던전 방의 총 개수를 조회합니다.
    /// </summary>
    public async Task<long> GetActiveRoomCountAsync(CancellationToken ct = default)
    {
        return await _database.SetLengthAsync(ActiveRoomsKey);
    }

    /// <summary>
    /// 던전 방의 상태(이름, 방장, 인원, 상태 등) 및 플레이어 목록을 업데이트합니다.
    /// </summary>
    /// <param name="room">업데이트할 방 객체</param>
    /// <returns>업데이트 성공 여부</returns>
    public async Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room, CancellationToken ct = default)
    {
        try
        {
            // 1. RoomId 검증
            if (room.RoomId <= 0)
                throw new InvalidOperationException("UpdateAsync는 기존 방만 업데이트 가능");
            
            // 2. 기존 방 조회 (존재 여부 확인)
            var existingRoom = await GetByIdAsync(room.RoomId, ct);
            if (existingRoom == null)
                return false;
            
            // 3. Transaction 시작
            var transaction = _database.CreateTransaction();
            
            // 4. 방 기본 정보 업데이트 (Hash)
            Task hashTask = transaction.HashSetAsync($"{RoomKey}:{room.RoomId}",
            [
                new HashEntry("RoomName", room.RoomName),
                new HashEntry("HostUserId", room.HostUserId),
                new HashEntry("MaxPlayers", room.MaxPlayers),
                new HashEntry("Status", room.Status.ToString())
            ]);
            
            Task? activeRoomsTask = null;
            if (room.Status == RoomStatus.Closed && existingRoom.Status != RoomStatus.Closed)
            {
                activeRoomsTask = transaction.SetRemoveAsync(ActiveRoomsKey, room.RoomId);
            }
            else if (room.Status != RoomStatus.Closed && existingRoom.Status == RoomStatus.Closed)
            {
                activeRoomsTask = transaction.SetAddAsync(ActiveRoomsKey, room.RoomId);
            }
            
            // 8. Transaction 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            // 9. 모든 Task 완료 대기
            var tasks = new List<Task> 
            { 
                hashTask
            };
            if (activeRoomsTask is not null)
                tasks.Add(activeRoomsTask);
        
            await Task.WhenAll(tasks);

            return true;

        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to update dungeon room {RoomId}", room.RoomId);
            throw;
        }
    }

    /// <summary>
    /// 지정된 방 ID에 해당하는 던전 방 정보를 Redis에서 영구 삭제합니다.
    /// </summary>
    /// <param name="roomId">삭제할 방 ID</param>
    /// <returns>삭제 성공 여부</returns>
    public async Task<bool> DeleteAsync(long roomId, CancellationToken ct = default)
    {
        try
        {
            var room = await GetByIdAsync(roomId, ct);
            if (room is null)
                return false;
        
            var transaction = _database.CreateTransaction();
        
            // 2. 방 데이터 삭제
            Task delRoomTask = transaction.KeyDeleteAsync($"{RoomKey}:{roomId}");
        
            // 3. 활성 목록에서 제거
            Task removeActiveTask = transaction.SetRemoveAsync(ActiveRoomsKey, roomId);
        
            // 6. Transaction 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            // 7. 모든 Task 완료 대기
            await Task.WhenAll(
                new[] { delRoomTask, removeActiveTask });
            
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to delete dungeon room {RoomId}", roomId);
            throw;
        }
    }

    public async Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, CancellationToken ct = default)
    {
        var room = await GetByIdAsync(roomId, ct);
        if (room is null)
            return JoinRoomAtomicResult.RoomNotFound;

        return room.Status == RoomStatus.Waiting
            ? JoinRoomAtomicResult.Success
            : JoinRoomAtomicResult.InvalidStatus;
    }

    /// <summary>
    /// Redis에서 조회한 Hash 데이터와 Set 데이터를 사용하여 DungeonRoom 도메인 객체로 변환합니다.
    /// </summary>
    private Domain.Entities.DungeonRoom? ParseDungeonRoomFromRedis(
        long roomId,
        HashEntry[] entries)
    {
        // 1. Hash를 Dictionary로 변환
        var dict = entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());

        // 2. 필수 필드 검증
        if (!dict.TryGetValue("RoomId", out var roomIdStr) ||
            !dict.TryGetValue("RoomName", out var roomName) ||
            !dict.TryGetValue("HostUserId", out var hostUserIdStr) ||
            !dict.TryGetValue("MaxPlayers", out var maxPlayersStr) ||
            !dict.TryGetValue("Status", out var statusStr) ||
            !dict.TryGetValue("CreatedAt", out var createdAtStr))
        {
            logger.LogWarning("Dungeon room {RoomId} has missing fields", roomId);
            return null;
        }

        // 3. 파싱
        if (!long.TryParse(roomIdStr, out var id))
        {
            logger.LogWarning("Invalid dungeon room id value {RoomIdValue}", roomIdStr);
            return null;
        }
    
        if (!long.TryParse(hostUserIdStr, out var hostUserId))
        {
            logger.LogWarning("Invalid host user id value {HostUserIdValue}", hostUserIdStr);
            return null;
        }
    
        if (!int.TryParse(maxPlayersStr, out var maxPlayers))
        {
            logger.LogWarning("Invalid max players value {MaxPlayersValue}", maxPlayersStr);
            return null;
        }
    
        if (!Enum.TryParse<RoomStatus>(statusStr, out var status))
        {
            logger.LogWarning("Invalid dungeon room status value {StatusValue}", statusStr);
            return null;
        }
    
        if (!DateTime.TryParse(createdAtStr, null, DateTimeStyles.RoundtripKind, out var createdAt))
        {
            logger.LogWarning("Invalid dungeon room created-at value {CreatedAtValue}", createdAtStr);
            return null;
        }

        // 4. FromRedis로 DungeonRoom 재구성
        return Domain.Entities.DungeonRoom.FromRedis(
            id, 
            roomName, 
            hostUserId, 
            maxPlayers, 
            status, 
            createdAt);
    }
}
