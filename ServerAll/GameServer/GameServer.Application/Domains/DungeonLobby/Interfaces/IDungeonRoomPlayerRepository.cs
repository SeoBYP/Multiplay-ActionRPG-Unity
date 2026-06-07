using DungeonRoomPlayerEntity = GameServer.Domain.Entities.DungeonRoomPlayer;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonRoomPlayerRepository
{
    Task<DungeonRoomPlayerEntity> CreateAsync(long roomId, long userId, CancellationToken ct = default);

    Task<List<DungeonRoomPlayerEntity>> GetPlayersByRoomIdAsync(long roomId, CancellationToken ct = default);

    /// <summary>여러 방의 플레이어를 한 번의 DB 쿼리로 조회 (방 목록 N+1 회피).</summary>
    Task<List<DungeonRoomPlayerEntity>> GetPlayersByRoomIdsAsync(IReadOnlyCollection<long> roomIds, CancellationToken ct = default);

    Task<DungeonRoomPlayerEntity?> GetByUserIdAsync(long userId, CancellationToken ct = default);

    Task<bool> RemoveAsync(long roomId, long userId, CancellationToken ct = default);

    Task<bool> RemoveByRoomIdAsync(long roomId, CancellationToken ct = default);
}
