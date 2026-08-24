namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 한 유저가 이미 다른(또는 같은) 방에 소속돼 있어 입장 기록을 만들 수 없을 때.
///
/// 저장소가 <c>dungeon_room_players.UserId</c> UNIQUE 제약 위반을 이 예외로 번역한다.
/// 서비스의 사전 검사(check-then-act)가 경합에서 뚫렸을 때의 최종 방어선이라,
/// Infrastructure 의 DB 예외 타입을 Application 까지 새어 나오게 하지 않으려고 둔다.
/// </summary>
public sealed class PlayerAlreadyInRoomException(long userId)
    : Exception($"User {userId} is already in a dungeon room")
{
    public long UserId { get; } = userId;
}
