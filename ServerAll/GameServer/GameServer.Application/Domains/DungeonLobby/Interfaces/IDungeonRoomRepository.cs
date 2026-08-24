namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

/// <summary>
/// 던전 방 저장소 인터페이스
/// </summary>
public interface IDungeonRoomRepository
{
    /// <summary>
    /// 던전 방 생성
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers, string mapId = "", CancellationToken ct = default);

    /// <summary>
    /// ID로 방 조회
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId, CancellationToken ct = default);

    /// <summary>
    /// 사용자 ID로 소속된 방 조회
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// 모든 활성 방 목록 조회
    /// </summary>
    Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync(CancellationToken ct = default);

    /// <summary>
    /// 활성 방 한 페이지와 전체 개수를 DB 에서 직접 잘라 온다(최신순).
    /// </summary>
    /// <remarks>
    /// 전량을 읽어 메모리에서 Skip/Take 하던 것을 DB 로 내린 것(백로그 B4).
    /// 정렬 키는 <c>RoomId</c> 내림차순 — 페이징의 전제인 안정 정렬을 DB 가 보장한다.
    /// </remarks>
    Task<ActiveRoomsPage> GetActiveRoomsPageAsync(int offset, int limit, CancellationToken ct = default);
    
    /// <summary>
    /// 활성 방 개수 조회
    /// </summary>
    Task<long> GetActiveRoomCountAsync(CancellationToken ct = default);

    /// <summary>
    /// 방 정보 업데이트
    /// </summary>
    Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room, CancellationToken ct = default);

    /// <summary>
    /// 방 삭제
    /// </summary>
    Task<bool> DeleteAsync(long roomId, CancellationToken ct = default);
    
    
    Task<JoinRoomAtomicResult> TryJoinRoomAsync(long userId, long roomId, CancellationToken ct = default);

    /// <summary>
    /// Redis 캐시만 무효화한다. DB는 건드리지 않는다.
    /// OutboxRepository처럼 DB만 업데이트하고 캐시를 갱신하지 않은 경우 호출한다.
    /// </summary>
    Task InvalidateCacheAsync(long roomId, CancellationToken ct = default);
}