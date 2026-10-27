using Script.System.GamePlayAbilitySystem;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

// 서버 권위 무적 창 + 마나: 쿨다운·마나 검증으로 C_Dodge 연사 = 영구 무적을 차단. 대시 위치는 C_Move 가 동기화.
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

        var actor = room.Actors.GetMember(session.UserId)?.Actor;
        if (actor is null)
            return;

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // 거부(쿨다운/마나)면 현재 값 → 클라가 예측으로 미리 깎은 마나를 되돌린다.
        bool began = actor.TryBeginDodge(nowMs, DodgeConfig.ManaCost);
        await session.SendPacketAsync(new S_PlayerMana
        {
            UserId = session.UserId,
            Mana = actor.Gas[EGameplayAttribute.Mana],
            MaxMana = actor.Gas.Max(EGameplayAttribute.Mana),
        }, ct);

        if (began)
            room.Sessions.Broadcast(new S_Dodge
            {
                UserId = session.UserId,
                DirX = packet.DirX,
                DirY = packet.DirY,
            });
    }
}
