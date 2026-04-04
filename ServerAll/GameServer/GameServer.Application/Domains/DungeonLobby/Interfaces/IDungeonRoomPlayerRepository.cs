using DungeonRoomPlayerEntity = GameServer.Domain.Entities.DungeonRoomPlayer;

namespace GameServer.Application.Domains.DungeonLobby.Interfaces;

public interface IDungeonRoomPlayerRepository
{
    Task<DungeonRoomPlayerEntity> CreateAsync(long roomId, long userId, CancellationToken ct = default);

    Task<List<DungeonRoomPlayerEntity>> GetPlayersByRoomIdAsync(long roomId, CancellationToken ct = default);

    Task<DungeonRoomPlayerEntity?> GetByUserIdAsync(long userId, CancellationToken ct = default);

    Task<bool> RemoveAsync(long roomId, long userId, CancellationToken ct = default);

    Task<bool> RemoveByRoomIdAsync(long roomId, CancellationToken ct = default);
}
