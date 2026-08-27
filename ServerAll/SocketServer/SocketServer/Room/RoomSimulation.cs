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

/// <summary>
/// 방 하나의 <b>한 틱 시뮬레이션</b>. 액터를 진행시키고(<see cref="Actor.Tick"/>) 브로드캐스트할 패킷을 만든다.
///
/// <para><b>진행 판정을 모른다</b>: 누가 다운됐는지·던전이 실패했는지는 방의 일이다.
/// 여기서는 "이번 틱에 HP 0 이 된 참가자"만 돌려주고, 그 뒤(S_PlayerDead·S_DungeonFailed)는 Room 이 붙인다.
/// 그래서 시뮬레이션은 <c>_outcome</c>·다운 집계 같은 방 상태를 건드리지 않는다.</para>
///
/// <para><b>액터는 패킷을 모르고, 시뮬레이션은 전송을 모른다</b> — 계산은 <see cref="MonsterAiMath"/>(순수),
/// 상태는 <see cref="ActorStore"/>, 전송은 호출자(RoomTickService)가 락 밖에서.</para>
/// </summary>
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

    /// <summary>
    /// 한 틱 진행. 모든 액터를 한 번 순회한다.
    /// </summary>
    /// <returns>
    /// Packets = 방에 브로드캐스트할 것들(상태·발동·효과).
    /// DownedUserIds = 이번 틱에 HP 0 이 된 참가자 — <b>다운 확정은 호출자가 한다</b>.
    /// </returns>
    public (List<Packet> Packets, List<long> DownedUserIds) Tick(float dt, long nowMs, MapBounds bounds)
    {
        var outPackets = new List<Packet>();
        var downed = new List<long>();

        lock (_actors.SyncRoot)
        {
            // 타깃 자격은 **방 관리 정보**(미입장·끊김·다운)로 정해진다. 액터는 결과(IsTargetable)만 안다.
            // 미입장 제외가 없으면 입장 전에 몬스터가 죽여 S_PlayerDead 가 빈 방에 유실된다.
            var targetMembers = new List<RoomMember>();
            var targetPositions = new List<TargetPos>();
            foreach (var member in _actors.MembersLocked)
            {
                // 다운(Dead 태그)된 플레이어는 제외 — 죽은 플레이어를 몬스터가 계속 때리지 않도록.
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
                    // 플레이어: 마나 자연 회복 + 지속 Effect 만료. 다운 여부와 무관하게 진행한다.
                    EmitExpired(actor.Tick(dt, nowMs, targetPositions, bounds), outPackets);
                    continue;
                }

                if (monster.Gas.IsDead) continue;

                var decision = monster.Tick(dt, nowMs, targetPositions, bounds);
                EmitExpired(decision, outPackets);

                // dirty-flag: 위치·회전·HP·페이즈가 직전 송신과 같으면 생략 → Idle 경비 몬스터 트래픽 0.
                // 신규 입장자는 S_SpawnMonster 로스터로 최신 상태를 받으므로 유실 없음.
                if (monster.StateDirty())
                {
                    outPackets.Add(BuildMonsterState(
                        monster, monster.Gas[EGameplayAttribute.Health], monster.NextSeq()));
                    monster.MarkStateSent();
                }

                // 발동 결정은 몬스터가 이미 끝냈다(어빌리티 선택·쿨다운 커밋). 여기선 그것을 패킷·피해로 번역한다.
                if (decision.Cast is null)
                    continue;

                MonsterAttack(monster, targetMembers[decision.TargetIndex], decision.Cast, nowMs, outPackets, downed);
            }
        }

        return (outPackets, downed);
    }

    /// <summary>
    /// 만료된 지속 Effect 를 <c>S_RemoveEffect</c> 로 번역한다.
    /// <b>만료 판정은 액터가 이미 끝냈다</b> — 여기서는 시각을 다시 보지 않는다(권위가 두 곳으로 갈리지 않게).
    /// </summary>
    private static void EmitExpired(ActorTickResult result, List<Packet> outPackets)
    {
        var expired = result.ExpiredEffectIds;
        if (expired is null)
            return;

        for (int i = 0; i < expired.Count; i++)
            outPackets.Add(new S_RemoveEffect { InstanceId = expired[i] });
    }

    /// <summary>몬스터 1마리의 발동·피해 처리(호출자가 저장소 락을 잡고 있어야 한다).</summary>
    private void MonsterAttack(
        MonsterActor monster, RoomMember target, AbilityDef chosen, long nowMs,
        List<Packet> outPackets, List<long> downed)
    {
        long targetUserId = target.UserId;
        long attackerActorId = monster.ActorId;

        // 발동 = "이 액터가 스킬을 썼다" 통합 연출 신호. i-frame 으로 빗나가도(헛스윙) 스윙 애니는 나가야 하므로
        // 데미지 판정(무적 return)보다 먼저 broadcast.
        outPackets.Add(new S_AbilityActivated { ActorId = attackerActorId, SkillId = chosen.NetworkId });

        _logger.LogInformation("[GameplayAbility] monster {MonsterId}(actor {ActorId}) 발동: '{AbilityId}' → user {UserId}",
            monster.MonsterId, attackerActorId, chosen.Id, targetUserId);

        // 회피 무적(i-frame): 무적 창 안이면 이 공격은 빗나간다. 쿨다운은 이미 소모 — 몬스터가 헛스윙한 것.
        if (target.Actor.IsInvulnerableAt(nowMs))
            return;

        // 데미지 = 어빌리티 BaseDamage(레벨 스케일) − 대상 Defense. 플레이어→몬스터와 동일 산식.
        // 스탯은 액터의 GAS 가 갖는다 — 호출부에 const 0 을 두지 않는다.
        int scaledBase = MonsterLevelScaling.Damage(chosen.BaseDamage, monster.Level);
        int attackPower = monster.Gas[EGameplayAttribute.AttackPower];
        int defense = target.Actor.Gas[EGameplayAttribute.Defense];
        int finalDamage = StatCombatMath.MeleeDamage(scaledBase, attackPower, defense);
        var dmgMods = new[]
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive),
        };

        // 트레이스: 플레이어→몬스터와 **같은 산식·다른 입력**. 저작값이 아니라 실제 산식에 들어간 값을 찍는다.
        // 플레이어 HP 권위는 클라(결정론 lite)라 서버는 before/after 를 모른다 → 0.
        CombatTrace.Damage(
            CombatPath.MonsterToPlayer, CombatTrace.FormulaMelee,
            attackerActorId, target.Actor.ActorId,
            chosen.Id, chosen.NetworkId,
            scaledBase, attackPower, defense, finalDamage,
            targetHpBefore: 0, targetHpAfter: 0,
            recvMs: nowMs, judgeMs: nowMs, seq: 0); // 틱 경로 = 수신·판정이 같은 틱 시각

        outPackets.Add(new S_ApplyEffect
        {
            InstanceId = _nextEffectInstanceId(),
            EffectId = global::Server.PacketHandler.Handler.CombatHandler.AbilityDamageEffectId,
            TargetId = targetUserId,
            SourceId = attackerActorId,
            StartTick = nowMs,
            Stacks = 1,
            Amount = -finalDamage, // 서버 권위 Health 델타(클라가 그대로 적용)
        });

        // CC(상태이상): 어빌리티의 OnHitEffectIds(태그/CC 전용)를 데미지와 함께 부여한다.
        // Amount=0 = HP 변경 없는 상태태그(Duration+GrantedTags) → 클라 EffectReceiver 가 적용 → 입력/이동 게이트.
        //
        // **서버도 자기에게 건다.** 예전엔 브로드캐스트만 하고 서버 액터에는 아무 흔적이 없어서,
        // 서버의 IsActivationBlocked 는 항상 false 였고 만료 시각도 클라만 알았다 — CC 권위가 통째로 클라에 있었다.
        // 이제 서버가 활성 Effect 를 들고 만료를 틱에서 소유한다(S_RemoveEffect 는 그 결과의 통지일 뿐).
        foreach (var ccId in chosen.Timeline.OnHitEffectIds)
        {
            if (string.IsNullOrEmpty(ccId)) continue;

            int instanceId = _nextEffectInstanceId();
            var def = CombatEffectCatalog.Get(ccId);
            if (def is null)
            {
                // 카탈로그에 없는 id 를 조용히 흘려보내면 클라만 CC 를 알고 서버는 모르는 옛 상태로 되돌아간다.
                _logger.LogWarning("[GameplayEffect] 미등록 effectId '{EffectId}' — 어빌리티 '{AbilityId}' 의 CC 를 건너뛴다", ccId, chosen.Id);
                continue;
            }

            // **반환된 id 를 쓴다.** 스택 정책이 기존 인스턴스를 재사용하면 방금 뽑은 id 는 버려지고,
            // 그걸 그대로 브로드캐스트하면 나중에 만료가 알리는 id 와 짝이 어긋나 클라에 CC 가 영영 남는다.
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

        // 서버 권위 HP 누적 + 사망 직접 감지(클라 보고에 의존 안 함 → 불사 핵 차단).
        // 이미 저장소 락 안이므로 액터에 직접 적용하고, **다운 확정은 호출자에게 넘긴다**.
        target.Actor.Gas.ApplyModifiers(dmgMods);
        if (target.Actor.Gas.IsDead)
            downed.Add(targetUserId);
    }

    /// <summary>S_MonsterState 조립 — 틱과 피격 두 경로가 같은 모양을 쓰도록 한 곳에 모은다.</summary>
    public static S_MonsterState BuildMonsterState(MonsterActor monster, int hp, int seq) => new()
    {
        InstanceId = monster.InstanceId,
        PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
        RotY = monster.RotY,
        Hp = hp,
        Phase = (byte)monster.Phase,
        // 틱이 **먼저 만들어 둔 옛 HP 패킷**보다 이 패킷이 새 상태임을 클라에 알린다.
        // 도착 순서가 뒤집혀도 클라가 Seq 로 스테일을 버린다(근본 해법).
        Seq = seq,
    };
}
