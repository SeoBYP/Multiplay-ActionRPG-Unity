namespace GameServer.Domain.Interfaces.DungeonRoom;

public interface IDungeonRoomRepository
{
    Task<Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers);

    Task<Entities.DungeonRoom?> GetByIdAsync(long roomId);

    Task<Entities.DungeonRoom?> GetByUserIdAsync(long userId);

    Task<IEnumerable<Entities.DungeonRoom>> GetAllActiveRoomsAsync();
    Task<long> GetActiveRoomCountAsync();

    Task<bool> UpdateAsync(Entities.DungeonRoom room);

    Task<bool> DeleteAsync(long roomId);
}