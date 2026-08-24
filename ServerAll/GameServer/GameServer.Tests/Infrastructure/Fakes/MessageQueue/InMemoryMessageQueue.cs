using System.Threading.Channels;
using Shared.Infrastructure.MessageQueue;

namespace GameServer.Tests.Infrastructure.Fakes.MessageQueue;

public sealed class InMemoryMessageQueue<T> : IMessageQueue<T> where T : class
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public Task EnqueueAsync(T message)
        => _channel.Writer.WriteAsync(message).AsTask();

    public async IAsyncEnumerable<StreamMessage<T>> DequeueAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
            yield return StreamMessage<T>.WithoutAck(item);
    }
}
