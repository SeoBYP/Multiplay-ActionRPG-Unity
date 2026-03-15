namespace GameServer.Infrastructure.MessageQueue;

public interface IBroadcastChannel<T>
{
    // 발행 (XADD)
    Task PublishAsync(string channel, T message, CancellationToken ct = default);

    // 구독 (XREAD - 독립 읽기)
    IAsyncEnumerable<(string messageId, T message)> ReadAsync(
        string channel,
        string lastMessageId,   // 재연결 복구용
        CancellationToken ct = default);
}