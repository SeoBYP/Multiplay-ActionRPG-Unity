using GameServer.Application.Domains.Outbox;
using GameServer.Domain.Entities.Outbox;
using GameServer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameServer.Infrastructure.Domains.Outbox;

public class OutboxRepository(
    IDbContextFactory<GameServerDbContext> contextFactory,
    ILogger<OutboxRepository> logger) : IOutboxRepository
{
    public async Task AddWithRoomUpdateAsync(Domain.Entities.DungeonRoom room, OutboxMessage outboxMessage,
        CancellationToken ct)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            context.DungeonRooms.Update(room);

            context.OutboxMessages.Add(outboxMessage);
            await context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to add outbox message");
            throw;
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            return await context.OutboxMessages
                .Where(m => m.ProcessedAt == null)
                .OrderBy(m => m.CreatedAt)
                .Take(batchSize)
                .ToListAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to get unprocessed outbox messages");
            throw;
        }
    }

    public async Task MarkProcessedAsync(long id, CancellationToken ct)
    {
        try
        {
            await using var context = await contextFactory.CreateDbContextAsync(ct);
            var message = await context.OutboxMessages.FirstOrDefaultAsync(m => m.MessageId == id, ct);
            if (message is null)
            {
                throw new KeyNotFoundException($"Outbox message not found for id {id}");
            }
            message.MarkAsProcessed();
            await context.SaveChangesAsync(ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to mark outbox message as processed: {MessageId}", id);
            throw;
        }
    }
}