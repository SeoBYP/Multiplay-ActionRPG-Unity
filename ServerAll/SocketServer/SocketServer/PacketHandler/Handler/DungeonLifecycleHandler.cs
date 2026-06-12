using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 던전 라이프사이클(다운/실패) 핸들러.
///
/// **사망 감지의 권위는 서버**(authority-model §4): `Room.TickMonsters`가 서버 HP≤0 을 직접 감지해
/// S_PlayerDead 를 발행한다. 이 C_PlayerDead 핸들러는 **클라 예측의 하위호환/보조 경로**일 뿐 —
/// `MarkPlayerDowned` 의 NewlyDowned 로 **중복 발화를 dedup**(서버 틱이 먼저 감지했으면 여기선 무시).
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
        var (newlyDowned, failClaimed) = room.MarkPlayerDowned(session.UserId);
        if (newlyDowned)
            room.Broadcast(new S_PlayerDead { UserId = session.UserId });
        if (failClaimed)
            room.Broadcast(new S_DungeonFailed { RoomId = room.RoomId });

        return ValueTask.CompletedTask;
    }
}
