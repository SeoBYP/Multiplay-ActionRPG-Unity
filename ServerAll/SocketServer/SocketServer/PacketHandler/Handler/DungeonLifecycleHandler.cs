using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 던전 라이프사이클(다운/실패) 핸들러.
///
/// **사망 감지의 권위는 서버**: `Room.Tick` 이 서버 HP≤0 을 직접 감지해 S_PlayerDead 를 발행한다.
/// 이 C_PlayerDead 핸들러는 클라 예측의 보조 경로일 뿐 — `MarkDowned` 의 NewlyDowned 로 중복 발화를 dedup
/// (서버 틱이 먼저 감지했으면 여기선 무시).
///
/// <b>이 패킷만으로는 다운되지 않는다</b> — `MarkDowned` 가 서버 HP 가 0 인지 확인한다(만피 자기신고 차단).
/// 클리어(CombatHandler)와는 `DungeonProgress` 의 단일 terminal 로 상호 배타.
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

        var (newlyDowned, failClaimed) = room.Progress.MarkDowned(session.UserId);
        if (newlyDowned)
            room.Sessions.Broadcast(new S_PlayerDead { UserId = session.UserId });
        if (failClaimed)
            room.Sessions.Broadcast(new S_DungeonFailed { RoomId = room.RoomId });

        return ValueTask.CompletedTask;
    }
}
