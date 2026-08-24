namespace Shared.Infrastructure.MessageQueue;

/// <summary>
/// 스트림에서 꺼낸 메시지 1건 + "처리에 성공했다"는 신호(ACK).
///
/// ACK 를 봉투에 담아 넘기는 이유: 핸들러는 큐 바깥(<see cref="ResilientStreamConsumer"/>)에서 돌기 때문에
/// 큐 스스로는 처리 성공 여부를 알 수 없다. 봉투를 받은 쪽이 **성공했을 때만** ACK 하면
/// 실패한 메시지는 PEL 에 남아 재배달된다(at-least-once).
///
/// 반대로 큐가 읽자마자 ACK 하면(at-most-once) 핸들러가 던지는 순간 메시지가 영구 소실된다 —
/// DB 순단 한 번이 곧 데이터 유실이었다.
/// </summary>
public sealed class StreamMessage<T>(T payload, Func<Task>? acknowledge = null)
{
    public T Payload { get; } = payload;

    /// <summary>처리 성공 후 호출한다. ACK 대상이 없는 큐(인메모리 Fake 등)면 no-op.</summary>
    public Task AcknowledgeAsync() => acknowledge?.Invoke() ?? Task.CompletedTask;

    /// <summary>ACK 개념이 없는 큐/테스트용 봉투.</summary>
    public static StreamMessage<T> WithoutAck(T payload) => new(payload);
}
