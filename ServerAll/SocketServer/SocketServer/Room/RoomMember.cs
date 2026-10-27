using Server.Actors;

namespace Server.Room;

// 방 관리 관심사(신원·배정·접속)만. 전투 상태는 PlayerActor — 소켓이 끊겨도 액터는 재접속 유예 동안 살아 있어야 한다.
public sealed class RoomMember
{
    public required long UserId { get; init; }
    public required string Nickname { get; init; }

    public required int SpawnIndex { get; init; }

    public required PlayerActor Actor { get; init; }

    // false = 아직 미입장(로딩 중). 미입장은 AI 타깃 제외 — 안 그러면 S_PlayerDead 가 빈 방에 유실된다.
    public bool HasJoined { get; set; }

    // 끊긴 시각(Unix ms), null = 접속 중. Room.ReconnectGraceMs 안에 재접속하면 액터 복귀, 만료되면 RoomTickService 스윕이 퇴장 확정.
    public long? DisconnectedAtMs { get; set; }
}
