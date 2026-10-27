using System.Numerics;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Server.Diagnostics;
using Server.Actors;
using Server.Monster;
using Shared.Infrastructure.Abilities;
using Shared.Infrastructure.Loot;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.PacketHandler.Handler;

/// <summary>
/// 전투 패킷 핸들러. C_Attack → 발동 게이트(차단·마나·콤보·쿨다운) → 서버 권위 적중 판정 → 몬스터 HP 차감.
/// </summary>
public static class CombatHandler
{
    /// <summary>피격 반경(캡슐 근사).</summary>
    public const float TargetRadius = 0.5f;

    /// <summary>콤보 간격 폴백(ms). 데이터 ComboChainMs 가 0 일 때만 쓴다 — 단계별 개별 쿨다운으론 연타를 못 막는다.</summary>
    public const int ComboMinIntervalMs = 300;

    /// <summary>콤보 간격 지터 허용치(ms). 패킷별 지연 차로 도착 간격이 짧아져도 정상 콤보가 거부되지 않게.</summary>
    public const int ComboCadenceToleranceMs = 100;

    /// <summary>콤보 단계 skillId(클라 ComboDriver 와 동일 규약: 2~5 = combo_a~d).</summary>
    public static bool IsComboSkill(int skillId) => skillId is 2 or 3 or 4 or 5;

    /// <summary>SkillId → 어빌리티 정의. 미등록이면 null → 발동 거부.</summary>
    public static AbilityDef? ResolveAbility(int skillId) => AbilityCatalog.Get(skillId);

    public static SkillTimeline? ResolveSkill(int skillId) => ResolveAbility(skillId)?.Timeline;

    [PacketHandler(typeof(C_Attack))]
    public static async ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return;

        var room = session.Room;
        if (room is null)
            return;

        long recvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long actorId = ActorIds.FromPlayer(session.UserId);

        var ability = ResolveAbility(packet.SkillId);
        if (ability is null)
        {
            CombatTrace.Gate(CombatGate.UnknownAbility, actorId, packet.SkillId, abilityId: "?", recvMs);
            return;
        }
        var skill = ability.Timeline;

        var attacker = room.Actors.GetMember(session.UserId)?.Actor;
        if (attacker is null)
            return;
        
        if (attacker.Gas.IsActivationBlocked)
        {
            CombatTrace.Gate(CombatGate.Blocked, actorId, ability.NetworkId, ability.Id, recvMs);
            return;
        }

        // 마나는 쿨다운보다 먼저 — 거부될 발동이 쿨다운 슬롯을 소모하지 않게.
        if (attacker.Gas[EGameplayAttribute.Mana] < skill.ManaCost)
        {
            CombatTrace.Gate(CombatGate.NoMana, actorId, ability.NetworkId, ability.Id, recvMs);
            await SendManaAsync(session, attacker, ct);
            return;
        }

        long startTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 콤보 즉시 연타(A→B→C 합산 폭딜) 차단.
        if (IsComboSkill(packet.SkillId)
            && !attacker.TryBeginComboAttack(startTick, skill.ComboChainMs, ComboMinIntervalMs, ComboCadenceToleranceMs))
        {
            CombatTrace.Gate(CombatGate.ComboCadence, actorId, ability.NetworkId, ability.Id, startTick);
            return;
        }

        if (!attacker.Gas.TryBeginAbility(ability.Id, skill.CooldownMs, startTick))
        {
            CombatTrace.Gate(CombatGate.OnCooldown, actorId, ability.NetworkId, ability.Id, startTick);
            return;
        }

        if (skill.ManaCost > 0)
        {
            attacker.Gas.TrySpendMana(skill.ManaCost);
            await SendManaAsync(session, attacker, ct);
        }

        // 게이트를 통과한 스윙만 원격 연출로 알린다.
        room.Sessions.Broadcast(new S_AbilityActivated
        {
            ActorId = ActorIds.FromPlayer(session.UserId),
            SkillId = packet.SkillId,
        });

        // 아군 오사 없음(Co-op PvE). PvP 도입 시 room.ApplyPlayerEffect 로 서버 HP 도 함께 차감해야 한다
        // — 클라 HP 만 깎으면 서버가 S_PlayerDead 를 발행하지 못한다.
        ApplyAttackToMonsters(session, room, ability, attacker, recvMs);
    }

    /// <summary>데미지 S_ApplyEffect 의 라벨. 수치는 ability.BaseDamage 를 서버가 Amount 로 실어 보낸다.</summary>
    public const string AbilityDamageEffectId = "ability_damage";

    private static Task SendManaAsync(Session session, PlayerActor actor, CancellationToken ct)
        => session.SendPacketAsync(new S_PlayerMana
        {
            UserId = session.UserId,
            Mana = actor.Gas[EGameplayAttribute.Mana],
            MaxMana = actor.Gas.Max(EGameplayAttribute.Mana),
        }, ct);

    /// <summary>ability.BaseDamage 를 공격력·방어력으로 스케일한 Health 모디파이어. on-hit CC 는 별도.</summary>
    public static List<GameplayAttributeModifier> BuildDamageMods(AbilityDef ability, int attackPower, int defense)
    {
        int finalDamage = StatCombatMath.MeleeDamage(ability.BaseDamage, attackPower, defense);
        return new List<GameplayAttributeModifier>
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive),
        };
    }

    private static void ApplyAttackToMonsters(Session session, global::Server.Room.Room room, AbilityDef ability, PlayerActor attacker, long recvMs)
    {
        var attackerPos = new Vector3(attacker.PosX, attacker.PosY, attacker.PosZ);
        bool anyKilled = false;

        foreach (var monster in room.Actors.Monsters())
        {
            if (monster.Gas.IsDead)
                continue;

            var targetPos = new Vector3(monster.PosX, monster.PosY, monster.PosZ);
            if (!HitboxMath.Overlaps(attackerPos, attacker.RotY, ability.Timeline.Hitbox, targetPos, TargetRadius))
                continue;

            var mods = BuildDamageMods(ability, attacker.Gas[EGameplayAttribute.AttackPower], monster.Gas[EGameplayAttribute.Defense]);
            int hpBefore = monster.Gas[EGameplayAttribute.Health];
            var (hit, newHp, dead) = room.Actors.DamageMonster(monster.InstanceId, mods);
            if (!hit)
                continue;

            int stateSeq = 0;
            if (dead)
            {
                anyKilled = true;
                room.Sessions.Broadcast(new S_MonsterDead { InstanceId = monster.InstanceId });
                SpawnDrops(room, monster);
            }
            else
            {
                stateSeq = monster.NextSeq();
                room.Sessions.Broadcast(global::Server.Room.RoomSimulation.BuildMonsterState(monster, newHp, stateSeq));
                // MarkStateSent() 를 일부러 생략 — 다음 틱이 무조건 재전송해 자가 교정한다(Seq 와 이중 안전망).
            }

            // 브로드캐스트 뒤에 남긴다 — 이 HP 를 실은 패킷의 Seq 가 클라 로그와의 상관키.
            CombatTrace.Damage(
                CombatPath.PlayerToMonster, CombatTrace.FormulaMelee,
                attacker.ActorId, monster.ActorId,
                ability.Id, ability.NetworkId,
                ability.BaseDamage, attacker.Gas[EGameplayAttribute.AttackPower], monster.Gas[EGameplayAttribute.Defense],
                finalDamage: hpBefore - newHp,
                hpBefore, newHp,
                recvMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), stateSeq);
        }

        // 전멸 시 1회만: 클라 결과화면 + GameServer 보상.
        if (anyKilled && room.Progress.TryMarkCleared())
        {
            long rewardExp = SpawnLayoutTable.Get(room.MapId).ExpReward;
            room.Sessions.Broadcast(new S_DungeonClear { RoomId = room.RoomId, RewardExp = rewardExp });
            session.RoomManager.PublishDungeonClear(room);
        }
    }

    /// <summary>사망 몬스터 드롭을 바닥에 스폰만 한다. 지급은 줍기(C_PickupItem) 확정 시.</summary>
    private static void SpawnDrops(global::Server.Room.Room room, MonsterActor monster)
    {
        var drops = DropTableCatalog.Roll(monster.MonsterId, Random.Shared, monster.Level);
        foreach (var drop in drops)
        {
            var ground = room.Loot.Spawn(drop.ItemId, drop.Qty, monster.PosX, monster.PosY, monster.PosZ);
            room.Sessions.Broadcast(new S_SpawnGroundItem
            {
                GroundId = ground.GroundId,
                ItemId = ground.ItemId,
                Qty = ground.Qty,
                PosX = ground.PosX, PosY = ground.PosY, PosZ = ground.PosZ,
            });
        }
    }
}
