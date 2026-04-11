namespace Shared.Infrastructure.Messages;

public sealed class GameStartRequestedMessage
{
    public long RoomId { get; init; }
    public IReadOnlyList<PlayerInfo> PlayerInfos { get; init; } = [];
    public string TraceId { get; init; } = "";
}

public sealed class PlayerInfo
{
    public long UserId { get; init; }
    public string Nickname { get; init; } = "";
    public int SpawnIndex { get; init; }
}
