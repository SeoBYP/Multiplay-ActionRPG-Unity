using StackExchange.Redis;

namespace GameServer.Infrastructure.MessageQueue;

public abstract class RedisBroadcastChannelBase<T> : IBroadcastChannel<T>
{
    protected readonly IConnectionMultiplexer Redis;
    protected readonly IDatabase Database;

    protected RedisBroadcastChannelBase(IConnectionMultiplexer redis)
    {
        Redis = redis;
        Database = redis.GetDatabase();
    }

    public abstract Task PublishAsync(string channel, T message, CancellationToken ct = default);
    public abstract IAsyncEnumerable<(string messageId, T message)> ReadAsync(string channel, string lastMessageId,
        CancellationToken ct = default);

    protected abstract ValueTask<string> SerializeMessage(T message);
    protected abstract ValueTask<T> DeserializeMessage(string data);
}