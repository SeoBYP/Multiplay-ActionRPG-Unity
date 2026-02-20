namespace GameServer.Infrastructure.Interfaces.DungeonRoom;

/// <summary>
/// 던전 방 저장소 인터페이스
/// </summary>
public interface IDungeonRoomRepository
{
    /// <summary>
    /// 던전 방 생성
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers);

    /// <summary>
    /// ID로 방 조회
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId);

    /// <summary>
    /// 사용자 ID로 소속된 방 조회
    /// </summary>
    Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId);

    /// <summary>
    /// 모든 활성 방 목록 조회
    /// </summary>
    Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync();
    
    /// <summary>
    /// 활성 방 개수 조회
    /// </summary>
    Task<long> GetActiveRoomCountAsync();

    /// <summary>
    /// 방 정보 업데이트
    /// </summary>
    Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room);

    /// <summary>
    /// 방 삭제
    /// </summary>
    Task<bool> DeleteAsync(long roomId);
}