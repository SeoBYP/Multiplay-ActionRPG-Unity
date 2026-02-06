using System.Globalization;
using GameServer.Domain.Entities;
using GameServer.Domain.Interfaces.DungeonRoom;
using StackExchange.Redis;

namespace GameServer.Infrastructure.Repositories.DungeonRoom;

public class DungeonRoomRepository(IConnectionMultiplexer connectionMultiplexer) : IDungeonRoomRepository
{
    private readonly IDatabase _database = connectionMultiplexer.GetDatabase();

    private const string RoomKey = "game:room";
    private const string ActiveRoomsKey = "game:room:active";
    private const string UserRoomMappingKey = "game:user:room";
    private const string RoomCounterKey = "game:room:id:counter";

    public async Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName,  int maxPlayers = 4)
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
            Task hashTask = transaction.HashSetAsync($"{RoomKey}:{roomId}",
            [
                new HashEntry("RoomId", roomId),
                new HashEntry("RoomName", roomName),
                new HashEntry("HostUserId", hostId),
                new HashEntry("MaxPlayers", room.MaxPlayers),
                new HashEntry("Status", room.Status.ToString()), // Enum → String
                new HashEntry("CreatedAt", room.CreatedAt.ToString("O")) // ISO 8601
            ]);

            // 5. 플레이어 목록 저장 (Set)
            // CurrentPlayers를 RedisValue[]로 변환
            var playerValues = room.CurrentPlayers
                .Select(p => (RedisValue)p)
                .ToArray();

            Task playersTask = transaction.SetAddAsync(
                $"{RoomKey}:{roomId}:players",
                playerValues);

            // 6. 활성 방 목록에 추가
            Task activeTask = transaction.SetAddAsync(ActiveRoomsKey, roomId);

            // 7. 사용자 → 방 매핑(Create에서는 Host만 있음)
            Task mappingTask = transaction.StringSetAsync(
                $"{UserRoomMappingKey}:{hostId}",
                roomId);

            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                throw new InvalidOperationException("Failed to create room");

            // 9. 모든 Task 완료 대기
            await Task.WhenAll(hashTask, playersTask, activeTask, mappingTask);
            return room;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId)
    {
        try
        {
            // 1. 방 기본 정보 조회 (Hash)
            var hashTask = _database.HashGetAllAsync($"{RoomKey}:{roomId}");

            // 2. 플레이어 목록 조회 (Set)
            var playersTask = _database.SetMembersAsync($"{RoomKey}:{roomId}:players");

            await Task.WhenAll(hashTask, playersTask);

            var entries = await hashTask;
            var players = await playersTask;

            // 3. 데이터 없으면 null 반환
            if (entries.Length == 0)
                return null;

            // 4. DungeonRoom 파싱
            return ParseDungeonRoomFromRedis(roomId, entries, players);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId)
    {
        try
        {
            var roomIdValue  = await _database.StringGetAsync($"{UserRoomMappingKey}:{userId}");
            // 매핑 정보 없음
            if (!roomIdValue.HasValue || roomIdValue.IsNullOrEmpty)
                return null;
            
            // long 파싱 안됨
            if (!long.TryParse(roomIdValue.ToString(), out var roomIdParsed))
                return null;
            
            return await GetByIdAsync(roomIdParsed);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync()
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

            var playersTasks = roomIds
                .Select(roomId => batch.SetMembersAsync($"{RoomKey}:{roomId}:players"))  // ← batch 사용!
                .ToList();
            
            batch.Execute();

            // 3. 결과 파싱
            var rooms = new List<Domain.Entities.DungeonRoom>();
            for (int i = 0; i < roomIds.Length; i++)
            {
                var entries = await roomTasks[i];
                if (entries.Length == 0)
                    continue;
                
                var players = await playersTasks[i];
                // players가 0이어도 방은 존재할 수 있음 (모두 퇴장 직전)
            
                var room = ParseDungeonRoomFromRedis(
                    long.Parse(roomIds[i].ToString()), 
                    entries, 
                    players);
            
                if (room is not null)
                    rooms.Add(room);
            }

            return rooms;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<long> GetActiveRoomCountAsync()
    {
        return await _database.SetLengthAsync(ActiveRoomsKey);
    }

    public async Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room)
    {
        try
        {
            // 1. RoomId 검증
            if (room.RoomId <= 0)
                throw new InvalidOperationException("UpdateAsync는 기존 방만 업데이트 가능");
            
            // 2. 기존 방 조회 (존재 여부 확인)
            var existingRoom = await GetByIdAsync(room.RoomId);
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
                new HashEntry("Status", room.Status.ToString()),
            ]);
            
            // 5. 플레이어 목록 업데이트 (기존 삭제 후 재추가)
            var playersKey = $"{RoomKey}:{room.RoomId}:players";
            
            // 기존 플레이어 목록 삭제
            Task deletePlayersTask = transaction.KeyDeleteAsync(playersKey);
        
            // 새 플레이어 목록 추가
            var playerValues = room.CurrentPlayers
                .Select(p => (RedisValue)p)
                .ToArray();
            
            Task addPlayersTask = playerValues.Length > 0
                ? transaction.SetAddAsync(playersKey, playerValues)
                : Task.CompletedTask;
        
            // 6. 사용자 → 방 매핑 업데이트
            // 기존 플레이어들의 매핑 삭제
            var deleteMappingTasks = existingRoom.CurrentPlayers
                .Select(userId => transaction.KeyDeleteAsync($"{UserRoomMappingKey}:{userId}"))
                .ToArray();

            
            // 새 플레이어들의 매핑 추가
            var addMappingTasks = room.CurrentPlayers
                .Select(userId => transaction.StringSetAsync(
                    $"{UserRoomMappingKey}:{userId}", 
                    room.RoomId))
                .ToArray();
            
            // 7. Closed 상태면 활성 목록에서 제거
            Task? removeActiveTask = null;
            if (room.Status == RoomStatus.Closed && existingRoom.Status != RoomStatus.Closed)
            {
                removeActiveTask = transaction.SetRemoveAsync(ActiveRoomsKey, room.RoomId);
            }
            
            // 8. Transaction 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
        
            // 9. 모든 Task 완료 대기
            var tasks = new List<Task> 
            { 
                hashTask, 
                deletePlayersTask, 
                addPlayersTask 
            };
            tasks.AddRange(deleteMappingTasks);
            tasks.AddRange(addMappingTasks);
            if (removeActiveTask is not null)
                tasks.Add(removeActiveTask);
        
            await Task.WhenAll(tasks);

            return true;

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(long roomId)
    {
        try
        {
            // 1. 먼저 방 정보 조회 (플레이어 목록 필요)
            var room = await GetByIdAsync(roomId);
            if (room == null)
                return false;
        
            var transaction = _database.CreateTransaction();
        
            // 2. 방 데이터 삭제
            Task delRoomTask = transaction.KeyDeleteAsync($"{RoomKey}:{roomId}");
        
            // 3. 활성 목록에서 제거
            Task removeActiveTask = transaction.SetRemoveAsync(ActiveRoomsKey, roomId);
        
            // 4. 플레이어 목록 삭제
            Task delPlayersTask = transaction.KeyDeleteAsync($"{RoomKey}:{roomId}:players");
        
            // 5. 모든 플레이어의 UserId → RoomId 매핑 삭제
            var delMappingTasks = room.CurrentPlayers
                .Select(userId => transaction.KeyDeleteAsync($"{UserRoomMappingKey}:{userId}"))
                .ToArray();
            
            // 6. Transaction 실행
            bool committed = await transaction.ExecuteAsync();
            if (!committed)
                return false;
            
            // 7. 모든 Task 완료 대기
            await Task.WhenAll(
                new[] { delRoomTask, removeActiveTask, delPlayersTask }
                    .Concat(delMappingTasks));
            
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private Domain.Entities.DungeonRoom? ParseDungeonRoomFromRedis(
        long roomId,
        HashEntry[] entries,
        RedisValue[] players)
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
            Console.WriteLine($"DungeonRoom {roomId} has missing fields");
            return null;
        }

        // 3. 파싱
        if (!long.TryParse(roomIdStr, out var id))
        {
            Console.WriteLine($"Invalid RoomId: {roomIdStr}");
            return null;
        }
    
        if (!long.TryParse(hostUserIdStr, out var hostUserId))
        {
            Console.WriteLine($"Invalid HostUserId: {hostUserIdStr}");
            return null;
        }
    
        if (!int.TryParse(maxPlayersStr, out var maxPlayers))
        {
            Console.WriteLine($"Invalid MaxPlayers: {maxPlayersStr}");
            return null;
        }
    
        if (!Enum.TryParse<RoomStatus>(statusStr, out var status))
        {
            Console.WriteLine($"Invalid Status: {statusStr}");
            return null;
        }
    
        if (!DateTime.TryParse(createdAtStr, null, DateTimeStyles.RoundtripKind, out var createdAt))
        {
            Console.WriteLine($"Invalid CreatedAt: {createdAtStr}");
            return null;
        }

        // 4. 플레이어 목록 변환
        // 4. 플레이어 목록 변환
        var currentPlayers = new HashSet<long>(
            players.Select(p => (long)p)
        );
        
        // 5. FromRedis로 DungeonRoom 재구성
        return Domain.Entities.DungeonRoom.FromRedis(
            id, 
            roomName, 
            hostUserId, 
            maxPlayers, 
            status, 
            currentPlayers,
            createdAt);
    }
}