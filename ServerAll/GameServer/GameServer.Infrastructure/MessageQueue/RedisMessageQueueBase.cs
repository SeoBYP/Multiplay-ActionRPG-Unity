using StackExchange.Redis;

namespace GameServer.Infrastructure.MessageQueue;

public abstract class RedisMessageQueueBase<T> : IMessageQueue<T>
{
    protected readonly IConnectionMultiplexer Redis;
    protected readonly IDatabase Database;
    protected readonly string QueueKey;

    protected RedisMessageQueueBase(IConnectionMultiplexer redis, string queueKey)
    {
        Redis = redis;
        Database = redis.GetDatabase();
        QueueKey = queueKey;
    }


    public abstract Task EnqueueAsync(T message);
    public abstract IAsyncEnumerable<T> DequeueAllAsync(CancellationToken cancellationToken = default);
    
    protected abstract ValueTask<string> SerializeMessage(T message);
    protected abstract ValueTask<T> DeserializeMessage(string data);
}