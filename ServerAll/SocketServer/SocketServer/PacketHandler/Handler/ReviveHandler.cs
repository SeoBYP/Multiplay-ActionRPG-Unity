using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// Co-op 부활(2.5.2) 핸들러 — 서버 권위.
///
/// C_Revive 수신 → <see cref="Server.Room.Room.TryRevive"/> 로 거리·다운상태·미실패를 재검증(권위) →
/// 통과 시 대상 HP 부분복구 + _downed 제거 + S_PlayerRevived 방 브로드캐스트(원격 가시성).
/// 홀드(시전 채널)는 클라 UX — 서버는 게임의미 불변식만 본다. 거부돼도 무해(아무 것도 안 함).
/// </summary>
public static class ReviveHandler
{
    [PacketHandler(typeof(C_Revive))]
    public static ValueTask HandleRevive(Session session, C_Revive packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        var (ok, hp) = room.TryRevive(session.UserId, packet.TargetUserId);
        if (ok)
            room.Broadcast(new S_PlayerRevived { UserId = packet.TargetUserId, Hp = hp });

        return ValueTask.CompletedTask;
    }
}
