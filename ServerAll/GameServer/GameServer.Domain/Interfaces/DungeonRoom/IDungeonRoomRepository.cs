namespace GameServer.Domain.Entities;

public interface IDungeonRoomRepository
{
    Task<DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers = 4);

    Task<DungeonRoom?> GetByIdAsync(long roomId);

    Task<DungeonRoom?> GetByUserIdAsync(long userId);

    Task<IEnumerable<DungeonRoom>> GetAllActiveRoomsAsync();
    Task<long> GetActiveRoomCountAsync();

    Task<bool> UpdateAsync(DungeonRoom room);

    Task<bool> DeleteAsync(long roomId);
}