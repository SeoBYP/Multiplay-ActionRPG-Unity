namespace GameServer.Infrastructure.Interfaces.DungeonRoom;

public interface IDungeonRoomRepository
{
    Task<Domain.Entities.DungeonRoom?> CreateAsync(long hostId, string roomName, int maxPlayers);

    Task<Domain.Entities.DungeonRoom?> GetByIdAsync(long roomId);

    Task<Domain.Entities.DungeonRoom?> GetByUserIdAsync(long userId);

    Task<IEnumerable<Domain.Entities.DungeonRoom>> GetAllActiveRoomsAsync();
    Task<long> GetActiveRoomCountAsync();

    Task<bool> UpdateAsync(Domain.Entities.DungeonRoom room);

    Task<bool> DeleteAsync(long roomId);
}