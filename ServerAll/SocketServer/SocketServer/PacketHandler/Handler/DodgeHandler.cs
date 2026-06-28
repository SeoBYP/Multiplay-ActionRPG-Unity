using Script.System.GamePlayAbilitySystem;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 회피(Dodge) 패킷 핸들러 — 서버 권위 무적 프레임 + 마나.
///
/// C_Dodge 수신 → 시전자 PlayerState 에 무적 창 부여(쿨다운+마나 검증, 통과 시 마나 차감). 무적 동안
/// TickMonsters 가 그 플레이어에 대한 몬스터 공격 피해를 무시한다(<see cref="Server.Room.Room.TickMonsters"/>).
/// 쿨다운(DodgeConfig.CooldownMs)·마나(DodgeConfig.ManaCost)를 서버가 강제해 C_Dodge 연사=영구 무적/무한
/// 회피 치팅을 차단한다. 어느 시점이든 owner 에게 권위 마나를 정정 송신(클라 예측 차감 정합).
///
/// 대시 이동/위치는 기존 C_Move 로 동기화되므로 여기서 위치를 다루지 않는다(시간 창만 권위).
/// </summary>
public static class DodgeHandler
{
    [PacketHandler(typeof(C_Dodge))]
    public static async ValueTask HandleDodge(Session session, C_Dodge packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return;

        var room = session.Room;
        if (room is null)
            return;

        var state = room.GetPlayerState(session.UserId);
        if (state is null)
            return;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // 쿨다운+마나 통과 시 무적 창 부여 + 마나 차감(거부돼도 무해). 결과와 무관하게 owner 에게 권위 마나 정정:
        // 성공이면 차감 후 값, 거부(쿨다운/마나)면 현재 값 → 클라가 예측으로 미리 깎은 마나를 되돌린다.
        state.TryBeginDodge(nowMs, DodgeConfig.ManaCost);
        await session.SendPacketAsync(new S_PlayerMana
        {
            UserId = state.UserId,
            Mana = state.Mana,
            MaxMana = state.MaxMana,
        }, ct);
    }
}
