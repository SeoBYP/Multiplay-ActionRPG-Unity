namespace GameServer.Application.Domains.DungeonLobby;

/// <summary>
/// 유저가 이미 방에 소속돼 있어 입장 기록을 만들 수 없을 때. 저장소가 <c>dungeon_room_players.UserId</c> UNIQUE
/// 위반을 번역한 것으로, 서비스 사전 검사가 경합에서 뚫렸을 때의 최종 방어선이다 (DB 예외 타입을 Application 에 노출하지 않으려는 번역).
/// </summary>
public sealed class PlayerAlreadyInRoomException(long userId)
    : Exception($"User {userId} is already in a dungeon room")
{
    public long UserId { get; } = userId;
}
