using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 회피(Dodge) 패킷 핸들러 — 서버 권위 무적 프레임.
///
/// C_Dodge 수신 → 시전자 PlayerState 에 무적 창 부여(쿨다운 검증). 무적 동안 TickMonsters 가
/// 그 플레이어에 대한 몬스터 공격 피해를 무시한다(<see cref="Server.Room.Room.TickMonsters"/>).
/// 쿨다운(DodgeConfig.CooldownMs)을 서버가 강제해 C_Dodge 연사=영구 무적 치팅을 차단한다.
///
/// 대시 이동/위치는 기존 C_Move 로 동기화되므로 여기서 위치를 다루지 않는다(시간 창만 권위).
/// </summary>
public static class DodgeHandler
{
    [PacketHandler(typeof(C_Dodge))]
    public static ValueTask HandleDodge(Session session, C_Dodge packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        var state = room.GetPlayerState(session.UserId);
        if (state is null)
            return ValueTask.CompletedTask;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        state.TryBeginDodge(nowMs); // 쿨다운 통과 시 무적 창 부여(거부돼도 무해).

        return ValueTask.CompletedTask;
    }
}
