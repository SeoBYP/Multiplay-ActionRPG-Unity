namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonRoomRepository
{
    Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default);

    Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default);

    Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default);

    Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default);

    /// <summary>활성 방 한 페이지와 전체 개수를 DB 에서 직접 잘라 온다. <c>RoomId</c> 내림차순(안정 정렬)이 페이징의 전제.</summary>
    Task<ActiveRoomsPage> GetActiveRoomsPageAsync(int offset, int limit, CancellationToken ct = default);
    
    Task<long> GetActiveRoomCountAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room, CancellationToken ct = default);

    Task<bool> DeleteAsync(long roomId, CancellationToken ct = default);
    
    
    Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, CancellationToken ct = default);

    /// <summary>Redis 캐시만 무효화한다(DB 불변). OutboxRepository 처럼 DB 만 갱신한 경로 뒤에 호출.</summary>
    Task InvalidateCacheAsync(long roomId, CancellationToken ct = default);
}