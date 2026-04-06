using System.Threading.Channels;
using Shared.Infrastructure.MessageQueue;

namespace GameServer.Tests.Infrastructure.Fakes.MessageQueue;

public sealed class InMemoryMessageQueue<T> : IMessageQueue<T>
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public Task EnqueueAsync(T message)
        => _channel.Writer.WriteAsync(message).AsTask();

    public IAsyncEnumerable<T> DequeueAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
