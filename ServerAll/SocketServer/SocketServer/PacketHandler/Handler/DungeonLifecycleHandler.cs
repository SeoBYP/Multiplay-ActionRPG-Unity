using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

// 사망 감지 권위는 서버(Room.Tick). 이 핸들러는 예측 보조 — MarkDowned 가 서버 HP 0 확인(만피 자기신고 차단) + NewlyDowned 로 중복 발화 dedup.
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
