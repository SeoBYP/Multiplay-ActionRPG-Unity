using Server.Actors;

namespace Server.Room;

/// <summary>
/// 방 참가자 1명. <b>방 관리 관심사만</b> 갖는다 — 신원·배정·접속 상태.
/// 전투 상태(위치·HP·마나·쿨다운)는 <see cref="PlayerActor"/> 소유다.
///
/// <para>둘을 가른 이유는 <b>수명이 다르기 때문</b>이다. 소켓이 끊겨도 액터는 재접속 유예 동안
/// 그대로 살아 있어야 원래 자리로 복귀한다. 한 클래스에 섞여 있을 땐 그 규칙을 주석으로만 설명할 수 있었다.</para>
/// </summary>
public sealed class RoomMember
{
    public required long UserId { get; init; }
    public required string Nickname { get; init; }

    /// <summary>게임 시작 시 배정된 스폰 슬롯. 클라 결정론 스폰 입력으로 전달된다.</summary>
    public required int SpawnIndex { get; init; }

    /// <summary>이 참가자의 캐릭터. 참조는 단방향(Actor 는 RoomMember 를 모른다).</summary>
    public required PlayerActor Actor { get; init; }

    /// <summary>
    /// 소켓 세션이 실제로 입장(C_PlayerJoin 성공)했는가. false = 상태만 미리 만들어 두고 아직 미입장(로딩 중).
    /// 미입장 플레이어는 AI 타깃에서 제외한다 — 들어오지도 않은 플레이어가 죽으면 S_PlayerDead 가 빈 방에 유실된다.
    /// </summary>
    public bool HasJoined { get; set; }

    /// <summary>
    /// 크래시/끊김으로 세션이 사라진 시각(Unix ms). null = 접속 중.
    /// 유예 창(<see cref="Room.ReconnectGraceMs"/>) 안에 재접속하면 보존된 액터로 즉시 복귀하고,
    /// 만료되면 RoomTickService 스윕이 영구 퇴장으로 확정한다.
    /// </summary>
    public long? DisconnectedAtMs { get; set; }
}
