using Microsoft.Extensions.Logging;
using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Combat;
using Server.Diagnostics;
using Server.Monster;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Monsters;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Room;

// 전송은 호출자(RoomTickService)가 락 밖에서. 다운 확정(S_PlayerDead·실패)은 Room 이 한다.
public sealed class RoomSimulation
{
    private readonly ActorStore _actors;
    private readonly Func<int> _nextEffectInstanceId;
    private readonly ILogger _logger;

    public RoomSimulation(ActorStore actors, Func<int> nextEffectInstanceId, ILogger logger)
    {
        _actors = actors;
        _nextEffectInstanceId = nextEffectInstanceId;
        _logger = logger;
    }

    public (List<Packet> Packets, List<long> DownedUserIds) Tick(float dt, long nowMs, MapBounds bounds)
    {
        var outPackets = new List<Packet>();
        var downed = new List<long>();

        lock (_actors.SyncRoot)
        {
            // 미입장 제외가 없으면 입장 전에 몬스터가 죽여 S_PlayerDead 가 빈 방에 유실된다.
            var targetMembers = new List<RoomMember>();
            var targetPositions = new List<TargetPos>();
            foreach (var member in _actors.MembersLocked)
            {
                bool targetable = member.HasJoined
                                  && member.DisconnectedAtMs is null
                                  && !member.Actor.Gas.HasTag(GameplayTags.Dead);
                member.Actor.IsTargetable = targetable;
                if (!targetable) continue;

                targetMembers.Add(member);
                targetPositions.Add(new TargetPos(member.Actor.PosX, member.Actor.PosZ));
            }

            foreach (var actor in _actors.ActorsLocked)
            {
                if (actor is not MonsterActor monster)
                {
                    EmitExpired(actor.Tick(dt, nowMs, targetPositions, bounds), outPackets);
                    continue;
                }

                if (monster.Gas.IsDead) continue;

                var decision = monster.Tick(dt, nowMs, targetPositions, bounds);
                EmitExpired(decision, outPackets);

                // dirty-flag: 직전 송신과 같으면 생략(신규 입장자는 S_SpawnMonster 로스터로 받는다).
                if (monster.StateDirty())
                {
                    outPackets.Add(BuildMonsterState(
                        monster, monster.Gas[EGameplayAttribute.Health], monster.NextSeq()));
                    monster.MarkStateSent();
                }

                if (decision.Cast is null)
                    continue;

                MonsterAttack(monster, targetMembers[decision.TargetIndex], decision.Cast, nowMs, outPackets, downed);
            }
        }

        return (outPackets, downed);
    }

    private static void EmitExpired(ActorTickResult result, List<Packet> outPackets)
    {
        var expired = result.ExpiredEffectIds;
        if (expired is null)
            return;

        for (int i = 0; i < expired.Count; i++)
            outPackets.Add(new S_RemoveEffect { InstanceId = expired[i] });
    }

    // 저장소 락 보유 상태에서 호출.
    private void MonsterAttack(
        MonsterActor monster, RoomMember target, AbilityDef chosen, long nowMs,
        List<Packet> outPackets, List<long> downed)
    {
        long targetUserId = target.UserId;
        long attackerActorId = monster.ActorId;

        // 헛스윙(i-frame)도 스윙 애니는 나가야 하므로 무적 판정보다 먼저 broadcast.
        outPackets.Add(new S_AbilityActivated { ActorId = attackerActorId, SkillId = chosen.NetworkId });

        _logger.LogInformation("[GameplayAbility] monster {MonsterId}(actor {ActorId}) 발동: '{AbilityId}' → user {UserId}",
            monster.MonsterId, attackerActorId, chosen.Id, targetUserId);

        if (target.Actor.IsInvulnerableAt(nowMs))
            return;

        int scaledBase = MonsterLevelScaling.Damage(chosen.BaseDamage, monster.Level);
        int attackPower = monster.Gas[EGameplayAttribute.AttackPower];
        int defense = target.Actor.Gas[EGameplayAttribute.Defense];
        int finalDamage = StatCombatMath.MeleeDamage(scaledBase, attackPower, defense);
        var dmgMods = new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive),
        };

        // 플레이어 HP 권위는 클라라 서버는 before/after 를 모른다 → 0.
        CombatTrace.Damage(
            CombatPath.MonsterToPlayer, CombatTrace.FormulaMelee,
            attackerActorId, target.Actor.ActorId,
            chosen.Id, chosen.NetworkId,
            scaledBase, attackPower, defense, finalDamage,
            targetHpBefore: 0, targetHpAfter: 0,
            recvMs: nowMs, judgeMs: nowMs, seq: 0);

        outPackets.Add(new S_ApplyEffect
        {
            InstanceId = _nextEffectInstanceId(),
            EffectId = global::Server.PacketHandler.Handler.CombatHandler.AbilityDamageEffectId,
            TargetId = targetUserId,
            SourceId = attackerActorId,
            StartTick = nowMs,
            Stacks = 1,
            Amount = -finalDamage,
        });

        // 서버도 자기에게 건다 — 서버 액터가 만료를 소유한다(IsActivationBlocked).
        foreach (var ccId in chosen.Timeline.OnHitEffectIds)
        {
            if (string.IsNullOrEmpty(ccId)) continue;

            int instanceId = _nextEffectInstanceId();
            var def = CombatEffectCatalog.Get(ccId);
            if (def is null)
            {
                _logger.LogWarning("[GameplayEffect] 미등록 effectId '{EffectId}' — 어빌리티 '{AbilityId}' 의 CC 를 건너뛴다", ccId, chosen.Id);
                continue;
            }

            // 스택 정책이 기존 인스턴스를 재사용하면 새 id 는 버려진다 — 반환된 id 로 알려야 만료 통지와 짝이 맞는다.
            int appliedId = target.Actor.Gas.ApplyEffect(def, instanceId, nowMs);

            outPackets.Add(new S_ApplyEffect
            {
                InstanceId = appliedId,
                EffectId = ccId,
                TargetId = targetUserId,
                SourceId = attackerActorId,
                StartTick = nowMs,
                Stacks = 1,
                Amount = 0,
            });
        }

        // 사망은 서버가 직접 감지(불사 핵 차단). 다운 확정은 호출자.
        target.Actor.Gas.ApplyModifiers(dmgMods);
        if (target.Actor.Gas.IsDead)
            downed.Add(targetUserId);
    }

    public static S_MonsterState BuildMonsterState(MonsterActor monster, int hp, int seq) => new()
    {
        InstanceId = monster.InstanceId,
        PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
        RotY = monster.RotY,
        Hp = hp,
        Phase = (byte)monster.Phase,
        // 틱이 먼저 만든 옛 HP 패킷보다 새 상태 — 클라가 Seq 로 스테일을 버린다.
        Seq = seq,
    };
}
