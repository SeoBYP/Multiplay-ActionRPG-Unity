using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 전투 패킷 핸들러 (EF-2d 테스트 등급).
///
/// 현재: C_Attack 수신 → 대상에게 디버프 GameplayEffect 부여를 방에 브로드캐스트.
/// 서버가 InstanceId(권위)·StartTick(서버 시각)을 부여한다.
/// ※ 정밀 active-window 판정·HP 시뮬·SkillId→Effect 매핑(SkillTimeline)은 CA-3에서 대체.
/// </summary>
public static class CombatHandler
{
    /// <summary>테스트용 SkillId→Effect 매핑. 추후 SkillTimeline/카탈로그로 대체.</summary>
    public const string TestDebuffEffectId = "def_down_10";

    /// <summary>
    /// 공격 → 부여할 Effect 패킷 구성 (순수 함수, I/O 없음 — 단위 테스트 대상).
    /// </summary>
    public static S_ApplyEffect BuildAttackEffect(long attackerId, long targetId, int instanceId, long startTick)
    {
        return new S_ApplyEffect
        {
            InstanceId = instanceId,
            EffectId = TestDebuffEffectId,
            TargetId = targetId,
            SourceId = attackerId,
            StartTick = startTick,
            Stacks = 1,
        };
    }

    [PacketHandler(typeof(C_Attack))]
    public static ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        var apply = BuildAttackEffect(
            attackerId: session.UserId,
            targetId: packet.TargetId,
            instanceId: room.NextEffectInstanceId(),
            startTick: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // 대상 포함 전원에게 — 각 클라가 자신/원격 캐릭터 ASC에 적용.
        room.Broadcast(apply);

        return ValueTask.CompletedTask;
    }
}
