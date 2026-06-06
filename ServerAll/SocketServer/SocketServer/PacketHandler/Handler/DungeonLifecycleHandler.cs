using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 던전 라이프사이클(실패) 핸들러.
///
/// C_PlayerDead(클라 권위 HP 0 보고) → Room.TryMarkFailed 로 다운 집계 →
/// 참가자 전원 다운 시 S_DungeonFailed 를 방에 1회 브로드캐스트.
/// 클리어(CombatHandler)와는 Room._outcome 단일 terminal 로 상호 배타.
/// 실패는 보상이 없으므로 GameServer 통지(Redis Stream) 없음.
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

        if (room.TryMarkFailed(session.UserId))
            room.Broadcast(new S_DungeonFailed { RoomId = room.RoomId });

        return ValueTask.CompletedTask;
    }
}
