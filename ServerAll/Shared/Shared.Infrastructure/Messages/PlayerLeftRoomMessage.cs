namespace Shared.Infrastructure.Messages;

/// <summary>
/// SocketServer → GameServer: 플레이어가 방에서 퇴장할 때마다 발행(빈 방 여부와 무관).
///
/// GameServer는 이 메시지를 수신해:
///   - 해당 UserId의 DungeonRoomPlayer association을 제거하고(재로그인 복원 차단),
///   - <see cref="RoomEmptied"/>이면 방을 정리한다.
///
/// RoomEmptied는 SocketServer 메모리 기준 힌트이며, GameServer는 DB 잔여 인원으로 최종 판정한다.
/// </summary>
public sealed class PlayerLeftRoomMessage
{
    public long RoomId { get; init; }
    public long UserId { get; init; }
    public bool RoomEmptied { get; init; }
    public string TraceId { get; init; } = "";
}
