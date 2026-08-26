using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 던전 라이프사이클(다운/실패) 핸들러.
///
/// **사망 감지의 권위는 서버**(authority-model §4): `Room.Tick` 이 서버 HP≤0 을 직접 감지해
/// S_PlayerDead 를 발행한다. 이 C_PlayerDead 핸들러는 **클라 예측의 하위호환/보조 경로**일 뿐 —
/// `MarkPlayerDowned` 의 NewlyDowned 로 **중복 발화를 dedup**(서버 틱이 먼저 감지했으면 여기선 무시).
///
/// <b>이 패킷만으로는 다운되지 않는다</b> — `MarkPlayerDowned` 가 서버 HP 가 0 인지 확인한다.
/// 만피인 채로 자기신고해 몬스터 AI 타깃에서 빠지는 것을 막는다(다운도 서버 권위).
/// 클리어(CombatHandler)와는 Room._outcome 단일 terminal 로 상호 배타.
/// </summary>
public static class DungeonLifecycleHandler
{
    [PacketHandler(typeof(C_PlayerDead))]
    public static ValueTask HandlePlayerDead(Session session, C_PlayerDead packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        // 서버 틱이 이미 감지했으면 NewlyDowned=false → 아무 것도 안 함(dedup).
        var (newlyDowned, failClaimed) = room.Progress.MarkDowned(session.UserId);
        if (newlyDowned)
            room.Sessions.Broadcast(new S_PlayerDead { UserId = session.UserId });
        if (failClaimed)
            room.Sessions.Broadcast(new S_DungeonFailed { RoomId = room.RoomId });

        return ValueTask.CompletedTask;
    }
}
