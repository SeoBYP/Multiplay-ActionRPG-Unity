using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;

namespace GameServer.Application.Domains.Outbox;

public interface IOutboxRepository
{
    Task AddWithRoomUpdateAsync(DungeonRoom room, OutboxMessage outboxMessage, CancellationToken ct);
    
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct);
    
    Task MarkProcessedAsync(long id, CancellationToken ct);
}