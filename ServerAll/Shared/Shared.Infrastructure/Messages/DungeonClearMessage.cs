namespace Shared.Infrastructure.Messages;

/// <summary>
/// SocketServer → GameServer: 방의 몬스터가 전멸해 던전이 클리어되면 1회 발행.
///
/// GameServer는 이 메시지를 소비해(DungeonResultConsumer) 참가자에게 보상을 산정/지급한다.
/// 던전 식별은 MapId(스폰 레이아웃 키)로 한다.
/// </summary>
public sealed class DungeonClearMessage
{
    public long RoomId { get; init; }
    public string MapId { get; init; } = "";
    public long[] Participants { get; init; } = System.Array.Empty<long>();
    public string TraceId { get; init; } = "";
}
