using Shared.Infrastructure.MessageQueue;
using Shared.Infrastructure.Messages;

namespace GameServer.Tests.Infrastructure.Fakes.MessageQueue;

public sealed class FakeGameStartRequestedQueue(InMemoryMessageQueue<GameSessionReadyMessage> readyQueue)
    : IMessageQueue<GameStartRequestedMessage>
{
    public GameStartRequestedMessage? LastEnqueuedMessage { get; private set; }

    public async Task EnqueueAsync(GameStartRequestedMessage message)
    {
        LastEnqueuedMessage = message;
        await readyQueue.EnqueueAsync(new GameSessionReadyMessage
        {
            RoomId = message.RoomId,
            GameSessionId = 0,
            Host = "127.0.0.1",
            Port = 12345,
            TraceId = message.TraceId
        });
    }

    public IAsyncEnumerable<GameStartRequestedMessage> DequeueAllAsync(CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<GameStartRequestedMessage>();
}
