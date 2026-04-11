using System.Text.Json;
using GameServer.Application.Domains.DungeonLobby.Interfaces;
using GameServer.Application.Domains.Outbox;
using GameServer.Domain.Entities;
using GameServer.Domain.Entities.Outbox;
using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Tests.Infrastructure.Fakes.Repositories;

public class FakeOutboxRepository(
    IDungeonRoomRepository roomRepository,
    IMessageQueue<GameStartRequestedMessage> gameStartMessageQueue) : IOutboxRepository
{
    public async Task AddWithRoomUpdateAsync(DungeonRoom room, OutboxMessage outboxMessage, CancellationToken ct)
    {
        // 1. 룸 업데이트 (FakeDungeonRoomRepository에 위임)
        await roomRepository.UpdateAsync(room, ct);

        // 2. 메시지 즉시 발행 (OutboxPublisher 폴링 생략)
        if (outboxMessage.Topic == OutboxTopics.GameStartRequested)
        {
            var message = JsonSerializer.Deserialize<GameStartRequestedMessage>(outboxMessage.Payload);
            if (message != null)
            {
                await gameStartMessageQueue.EnqueueAsync(message);
            }
        }
    }

    public Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken ct)
    {
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(new List<OutboxMessage>());
    }

    public Task MarkProcessedAsync(long id, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
