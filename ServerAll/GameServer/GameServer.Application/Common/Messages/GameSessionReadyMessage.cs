namespace Shared.Infrastructure.Messages;

public sealed class GameSessionReadyMessage
{
    public long RoomId { get; init; }
    public long GameSessionId { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string TraceId { get; init; } = "";
}
