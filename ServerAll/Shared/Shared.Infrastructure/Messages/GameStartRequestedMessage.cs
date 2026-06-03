namespace Shared.Infrastructure.Messages;

public sealed class GameStartRequestedMessage
{
    public long RoomId { get; init; }
    public IReadOnlyList<PlayerInfo> PlayerInfos { get; init; } = [];
    public string TraceId { get; init; } = "";
    /// <summary>플레이할 맵 식별자. 스폰 레이아웃(spawn-layouts.json) 키와 대응.</summary>
    public string MapId { get; init; } = Spawn.MapIds.Default;
}

public sealed class PlayerInfo
{
    public long UserId { get; init; }
    public string Nickname { get; init; } = "";
    public int SpawnIndex { get; init; }
}
