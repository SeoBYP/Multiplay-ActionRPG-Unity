namespace Shared.Infrastructure.MessageQueue;

public interface IMessageQueue<T>
{
    Task EnqueueAsync(T message);

    /// <summary>
    /// 큐에 남아있는 모든 메시지를 비동기로 순차적으로 디큐.
    /// 봉투(<see cref="StreamMessage{T}"/>)로 돌려주므로 **처리에 성공한 소비자가 직접 ACK** 한다.
    /// </summary>
    IAsyncEnumerable<StreamMessage<T>> DequeueAllAsync(CancellationToken cancellationToken = default);
}