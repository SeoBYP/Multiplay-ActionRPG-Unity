using System.Numerics;
using Script.System.GamePlayAbilitySystem;
using Server.Player;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 전투 패킷 핸들러 (CA-3: 서버 권위 적중 판정).
///
/// C_Attack 수신 → 시전자/대상 위치(Room)로 `HitboxMath` 적중 재계산(권위) →
/// 적중 대상에 스킬의 OnHitEffectIds를 GameplayEffect로 부여(EF-2d `S_ApplyEffect` 재사용).
///
/// 권위 범위 = 적중 판정 + 효과 브로드캐스트. HP는 클라가 공유 카탈로그로 결정론 계산(§ "결정론 lite").
/// ※ active-window 정밀 타이밍(서버 틱)·서버 HP 추적은 후속. 현재는 수신 시점 위치로 즉시 판정.
/// </summary>
public static class CombatHandler
{
    /// <summary>플레이어 피격 반경(캡슐 근사). 추후 캐릭터 데이터화.</summary>
    public const float TargetRadius = 0.5f;

    private static readonly SkillCatalog _skills = new();

    /// <summary>SkillId → SkillTimeline. v1은 기본 스윙 고정(SkillId 매핑은 SkillTimeline 데이터화 시 확장).</summary>
    public static SkillTimeline? ResolveSkill(int skillId) => _skills.Get("basic_swing");

    /// <summary>
    /// 순수 적중 판정 — 시전자 위치/yaw 기준 hitbox와 겹치는 대상 userId 목록.
    /// (자기 자신 제외) 단위 테스트 대상.
    /// </summary>
    public static List<long> SelectHitTargets(
        SkillTimeline skill, PlayerState attacker, IReadOnlyList<PlayerState> candidates, float targetRadius)
    {
        var hits = new List<long>();
        var attackerPos = new Vector3(attacker.PosX, attacker.PosY, attacker.PosZ);

        foreach (var c in candidates)
        {
            if (c.UserId == attacker.UserId)
                continue;

            var targetPos = new Vector3(c.PosX, c.PosY, c.PosZ);
            if (HitboxMath.Overlaps(attackerPos, attacker.RotY, skill.Hitbox, targetPos, targetRadius))
                hits.Add(c.UserId);
        }

        return hits;
    }

    [PacketHandler(typeof(C_Attack))]
    public static ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return ValueTask.CompletedTask;

        var room = session.Room;
        if (room is null)
            return ValueTask.CompletedTask;

        var skill = ResolveSkill(packet.SkillId);
        if (skill is null)
            return ValueTask.CompletedTask;

        var states = room.GetAllPlayerStates();
        PlayerState? attacker = null;
        foreach (var s in states)
            if (s.UserId == session.UserId) { attacker = s; break; }
        if (attacker is null)
            return ValueTask.CompletedTask;

        var hits = SelectHitTargets(skill, attacker, states, TargetRadius);
        if (hits.Count == 0)
            return ValueTask.CompletedTask;

        long startTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var targetId in hits)
        {
            foreach (var effectId in skill.OnHitEffectIds)
            {
                room.Broadcast(new S_ApplyEffect
                {
                    InstanceId = room.NextEffectInstanceId(),
                    EffectId = effectId,
                    TargetId = targetId,
                    SourceId = session.UserId,
                    StartTick = startTick,
                    Stacks = 1,
                });
            }
        }

        return ValueTask.CompletedTask;
    }
}
