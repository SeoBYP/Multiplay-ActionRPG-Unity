using System.Numerics;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Server.Monster;
using Server.Player;
using Shared.Infrastructure.Loot;
using Shared.Infrastructure.Spawn;
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

    /// <summary>
    /// SkillId(패킷 int) → SkillTimeline. 데이터=임베디드 skills.json(클라 SkillDefinition SO 저작→bake, §2.5).
    /// int→문자열 id 매핑(패킷 계약 보존): 0=basic_swing, 1=heavy_swing …
    /// </summary>
    public static SkillTimeline? ResolveSkill(int skillId)
    {
        string id = skillId switch
        {
            1 => "heavy_swing",
            _ => "basic_swing",
        };
        return Shared.Infrastructure.Skills.SkillCatalog.Get(id);
    }

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
    public static async ValueTask HandleAttack(Session session, C_Attack packet, CancellationToken ct)
    {
        if (session.UserId <= 0)
            return;

        var room = session.Room;
        if (room is null)
            return;

        var skill = ResolveSkill(packet.SkillId);
        if (skill is null)
            return;

        var states = room.GetAllPlayerStates();
        PlayerState? attacker = null;
        foreach (var s in states)
            if (s.UserId == session.UserId) { attacker = s; break; }
        if (attacker is null)
            return;

        // 0a) 마나 게이트(권위). 부족하면 발동 거부 + owner 에게 현재 마나 정정(클라 예측 차감 되돌림).
        //     쿨다운보다 먼저 본다 — 마나 부족으로 거부될 발동이 쿨다운 슬롯을 소모하지 않게.
        if (attacker.Mana < skill.ManaCost)
        {
            await SendManaAsync(session, attacker, ct);
            return;
        }

        // 0b) 서버 발동 게이트(권위 쿨다운). 쿨다운 중이면 발동 거부 → 데미지 0.
        //     클라가 C_Attack 을 연사해도 서버가 cadence 를 강제해 폭딜 치팅을 막는다.
        long startTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (!attacker.TryBeginSkill(packet.SkillId, skill.CooldownMs, startTick))
            return;

        // 0c) 마나 차감(권위) + owner 정정. 무료 스킬(basic_swing, cost 0)은 차감/정정 패킷 모두 생략.
        if (skill.ManaCost > 0)
        {
            attacker.TrySpendMana(skill.ManaCost);
            await SendManaAsync(session, attacker, ct);
        }

        // 1) 플레이어 피격 → S_ApplyEffect (HP 는 클라가 공유 카탈로그로 결정론 계산) — 기존
        var hits = SelectHitTargets(skill, attacker, states, TargetRadius);
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

        // 2) 몬스터 피격 → 서버 권위 HP 차감(GAS) → S_MonsterState / S_MonsterDead — 신규(⑤)
        ApplyAttackToMonsters(session, room, skill, attacker);
    }

    /// <summary>owner(시전 세션)에게 현재 권위 마나를 정정 전송(차감/거부 시점). 리젠은 보내지 않는다.</summary>
    private static Task SendManaAsync(Session session, PlayerState state, CancellationToken ct)
        => session.SendPacketAsync(new S_PlayerMana
        {
            UserId = state.UserId,
            Mana = state.Mana,
            MaxMana = state.MaxMana,
        }, ct);

    /// <summary>
    /// 스킬 on-hit 효과의 Health 데미지를 스탯으로 스케일한다(2.4). 순수 — 단위 테스트 대상.
    /// Health 감소(데미지) 모디파이어만 `StatCombatMath.MeleeDamage(base, attackPower, defense)` 로 재계산하고,
    /// 그 외 모디파이어(회복·버프 등)는 그대로 둔다.
    /// </summary>
    public static List<GameplayAttributeModifier> ScaleDamageByStats(SkillTimeline skill, int attackPower, int defense)
    {
        var mods = new List<GameplayAttributeModifier>();
        foreach (var effectId in skill.OnHitEffectIds)
        {
            foreach (var m in CombatEffectCatalog.Resolve(effectId))
            {
                if (m.AttributeType == EGameplayAttribute.Health && m.ModifierType == EModifierType.Additive && m.Amount < 0)
                {
                    int finalDamage = StatCombatMath.MeleeDamage(-m.Amount, attackPower, defense);
                    mods.Add(GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive));
                }
                else
                {
                    mods.Add(m);
                }
            }
        }
        return mods;
    }

    /// <summary>
    /// 시전자 hitbox 와 겹치는 몬스터에 on-hit 효과를 GAS Health 모디파이어로 적용(서버 권위).
    /// 사망 시 S_MonsterDead, 생존 시 갱신된 HP 를 S_MonsterState 로 즉시 브로드캐스트.
    /// </summary>
    private static void ApplyAttackToMonsters(Session session, global::Server.Room.Room room, SkillTimeline skill, PlayerState attacker)
    {
        // 스탯 기반 데미지(2.4): 카탈로그의 Health 감소량을 base 로 보고 attacker.AttackPower 로 스케일.
        // 몬스터 defense=0(몬스터 방어 스탯은 미도입) — 몬스터→플레이어 Defense 는 증분3.
        var mods = ScaleDamageByStats(skill, attacker.AttackPower, defense: 0);
        if (mods.Count == 0)
            return;

        var attackerPos = new Vector3(attacker.PosX, attacker.PosY, attacker.PosZ);
        bool anyKilled = false;

        foreach (var monster in room.GetAllMonsters())
        {
            if (monster.IsDead)
                continue;

            var targetPos = new Vector3(monster.PosX, monster.PosY, monster.PosZ);
            if (!HitboxMath.Overlaps(attackerPos, attacker.RotY, skill.Hitbox, targetPos, TargetRadius))
                continue;

            var (hit, newHp, dead) = room.DamageMonster(monster.InstanceId, mods);
            if (!hit)
                continue;

            if (dead)
            {
                anyKilled = true;
                room.Broadcast(new S_MonsterDead { InstanceId = monster.InstanceId });
                SpawnDrops(room, monster);
            }
            else
            {
                room.Broadcast(new S_MonsterState
                {
                    InstanceId = monster.InstanceId,
                    PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
                    RotY = monster.RotY,
                    Hp = newHp,
                    Phase = (byte)monster.Phase,
                });
            }
        }

        // 클리어 감지: 이번 스윙으로 몬스터가 죽었고, 그 결과 전멸이면 1회만 발화.
        // S_DungeonClear(클라 결과화면) + DungeonClearMessage(GameServer 보상) 두 경로로 통지.
        if (anyKilled && room.TryMarkCleared())
        {
            // 표시용 보상 = 지급(GameServer)과 동일 Shared 카탈로그 값(검증 가능).
            long rewardExp = SpawnLayoutTable.Get(room.MapId).ExpReward;
            room.Broadcast(new S_DungeonClear { RoomId = room.RoomId, RewardExp = rewardExp });
            session.RoomManager.PublishDungeonClear(room);
        }
    }

    /// <summary>
    /// 몬스터 사망 시 drop roll(서버 권위) → 바닥 아이템 스폰 → S_SpawnGroundItem 브로드캐스트.
    /// roll 은 itemId 문자열 + 확률만 다루며(정의는 GameServer 소유), 지급은 줍기 확정 시 별도 경로(loot-drop.md §1).
    /// 자동 지급이 아니라 월드에 떨어뜨리기만 한다 — 플레이어가 줍기(C_PickupItem)로 "먹을지" 선택.
    /// </summary>
    private static void SpawnDrops(global::Server.Room.Room room, MonsterState monster)
    {
        var drops = DropTableCatalog.Roll(monster.MonsterId, Random.Shared);
        foreach (var drop in drops)
        {
            var ground = room.SpawnGroundItem(drop.ItemId, drop.Qty, monster.PosX, monster.PosY, monster.PosZ);
            room.Broadcast(new S_SpawnGroundItem
            {
                GroundId = ground.GroundId,
                ItemId = ground.ItemId,
                Qty = ground.Qty,
                PosX = ground.PosX, PosY = ground.PosY, PosZ = ground.PosZ,
            });
        }
    }
}
