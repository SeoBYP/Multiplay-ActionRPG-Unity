namespace Shared.Infrastructure.Messages;

public sealed class GameStartRequestedMessage
{
    public long RoomId { get; init; }
    public IReadOnlyList<long> PlayerIds { get; init; } = [];
    public string TraceId { get; init; } = "";
}
