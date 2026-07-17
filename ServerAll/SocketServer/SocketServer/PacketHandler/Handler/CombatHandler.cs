using System.Numerics;
using Script.System.GamePlayAbilitySystem;
using Server.Combat;
using Server.Diagnostics;
using Server.Monster;
using Server.Player;
using Shared.Infrastructure.Abilities;
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
    /// 콤보 cadence 의 <b>최소 안전값</b>(ms). 진실원은 스킬 데이터(<c>SkillTimeline.ComboChainMs</c>, skills.json) —
    /// 이 상수는 데이터가 0(저작 누락)일 때만 쓰이는 폴백이다. 콤보는 단계마다 skillId 가 달라(2/3/4)
    /// **개별 쿨다운으로는 연타 버스트를 막지 못하므로**(각자 첫 발동) 최소한의 간격은 항상 강제한다.
    /// </summary>
    public const int ComboMinIntervalMs = 300;

    /// <summary>
    /// 콤보 cadence 의 네트워크 지터 허용치(ms). 클라는 정확히 ComboChainMs 간격으로 보내지만 패킷별 지연이 달라
    /// <b>서버 도착 간격이 그보다 짧아질 수 있다</b>(예: 두 번째 패킷의 지연이 더 짧을 때) → 허용치가 없으면
    /// 정상 콤보가 거부돼 데미지가 유실된다. 이만큼 느슨하게 봐도 버스트(즉시 3연타) 차단에는 지장이 없다.
    /// </summary>
    public const int ComboCadenceToleranceMs = 100;

    /// <summary>콤보 단계 skillId 인가(클라 ComboDriver 와 동일 규약: 2=combo_a·3=combo_b·4=combo_c).</summary>
    public static bool IsComboSkill(int skillId) => skillId is 2 or 3 or 4;

    /// <summary>
    /// SkillId(패킷 int) → 어빌리티 정의. 데이터=임베디드 abilities.json(클라 `AbilityDefinition` SO 저작→bake, AC-B).
    ///
    /// int→어빌리티 매핑은 **데이터**(`AbilityDefinition.networkId`)다 — 과거의 하드코딩 switch 는 제거됐다.
    /// → 스킬을 추가해도 **서버 코드 수정이 필요 없다**(SO 저작 + Export + 서버 재빌드로 끝). 설계 = ability-so-authoring.md.
    /// 미등록 networkId 는 null → 호출자가 발동을 거부한다(조작된 SkillId 방어).
    /// </summary>
    public static AbilityDef? ResolveAbility(int skillId) => AbilityCatalog.Get(skillId);

    /// <summary>발동 판정 데이터(타임라인·hitbox·on-hit)만 필요한 호출자용 축약.</summary>
    public static SkillTimeline? ResolveSkill(int skillId) => ResolveAbility(skillId)?.Timeline;

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

        long recvMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long actorId = ActorIds.FromPlayer(session.UserId);

        var ability = ResolveAbility(packet.SkillId);
        if (ability is null)
        {
            CombatTrace.Gate(CombatGate.UnknownAbility, actorId, packet.SkillId, abilityId: "?", recvMs);
            return;
        }
        var skill = ability.Timeline;

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
            CombatTrace.Gate(CombatGate.NoMana, actorId, ability.NetworkId, ability.Id, recvMs);
            await SendManaAsync(session, attacker, ct);
            return;
        }

        long startTick = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 0b-1) 콤보 cadence 게이트(권위, **데이터 주도**). 직전 콤보 스윙의 SkillTimeline.ComboChainMs 가
        //       지나기 전의 다음 콤보 공격은 거부 → A→B→C 즉시 3연타(합산 폭딜) 치팅 차단.
        //       타이밍 진실원 = skills.json(SO 저작) — 클라 ComboDriver 가 쓰는 값과 동일하므로 서버·클라가 어긋나지 않는다.
        if (IsComboSkill(packet.SkillId)
            && !attacker.TryBeginComboAttack(startTick, skill.ComboChainMs, ComboMinIntervalMs, ComboCadenceToleranceMs))
        {
            CombatTrace.Gate(CombatGate.ComboCadence, actorId, ability.NetworkId, ability.Id, startTick);
            return;
        }

        // 0b-2) 서버 발동 게이트(권위 쿨다운). 쿨다운 중이면 발동 거부 → 데미지 0.
        //     클라가 C_Attack 을 연사해도 서버가 cadence 를 강제해 폭딜 치팅을 막는다.
        if (!attacker.TryBeginSkill(packet.SkillId, skill.CooldownMs, startTick))
        {
            CombatTrace.Gate(CombatGate.OnCooldown, actorId, ability.NetworkId, ability.Id, startTick);
            return;
        }

        // 0c) 마나 차감(권위) + owner 정정. 무료 스킬(basic_swing, cost 0)은 차감/정정 패킷 모두 생략.
        if (skill.ManaCost > 0)
        {
            attacker.TrySpendMana(skill.ManaCost);
            await SendManaAsync(session, attacker, ct);
        }

        // 0d) 원격 연출 브로드캐스트(AC 통합) — 서버 게이트(마나·쿨다운)를 통과한 스윙만 알린다.
        //     플레이어·몬스터 공용 S_AbilityActivated{ActorId} 한 파이프로 흡수(actor-combat-architecture §4.2).
        //     플레이어 ActorId = 양수 UserId. 연사 치팅이 원격 애니로 새지 않는다. 적중/데미지는 아래 S_ApplyEffect·S_MonsterState 가 담당.
        room.Broadcast(new S_AbilityActivated
        {
            ActorId = ActorIds.FromPlayer(session.UserId),
            SkillId = packet.SkillId,
        });

        // 1) 플레이어 피격 → S_ApplyEffect. AC-B 안B: 데미지 수치는 **어빌리티**(ability.BaseDamage)가 소유하고
        //    서버가 권위 델타를 Amount 로 실어 보낸다(effect 는 "즉발 피해" 형태만 주는 라벨).
        //    ※ 대상 Defense·시전자 AttackPower 는 여기서 반영하지 않는다 — 기존 동작(플랫 피해) 보존.
        //      플레이어→플레이어를 스탯 스케일로 바꾸는 것은 별도 밸런스 결정(B5 는 출처 일원화만).
        var hits = SelectHitTargets(skill, attacker, states, TargetRadius);
        foreach (var targetId in hits)
        {
            // 트레이스(AC-C1a): 이 경로만 **산식을 경유하지 않는다**(flat). AP/DEF 가 0 으로 찍히는 게 아니라
            // 애초에 입력이 아니라는 뜻 → FormulaFlat 표기 자체가 AC-D2 비대칭의 증거다.
            CombatTrace.Damage(
                CombatPath.PlayerToPlayer, CombatTrace.FormulaFlat,
                actorId, ActorIds.FromPlayer(targetId),
                ability.Id, ability.NetworkId,
                baseDamage: ability.BaseDamage, attackPower: 0, defense: 0, finalDamage: ability.BaseDamage,
                targetHpBefore: 0, targetHpAfter: 0, // 플레이어 HP 권위는 클라(결정론 lite) — 서버가 모른다
                recvMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), seq: 0);

            room.Broadcast(new S_ApplyEffect
            {
                InstanceId = room.NextEffectInstanceId(),
                EffectId = AbilityDamageEffectId,
                TargetId = targetId,
                SourceId = session.UserId,
                StartTick = startTick,
                Stacks = 1,
                Amount = -ability.BaseDamage, // 서버 권위 Health 델타
            });

            // CC/상태이상 — 어빌리티의 on-hit(태그 전용). HP 변경 없음(Amount=0).
            foreach (var ccId in skill.OnHitEffectIds)
            {
                if (string.IsNullOrEmpty(ccId)) continue;
                room.Broadcast(new S_ApplyEffect
                {
                    InstanceId = room.NextEffectInstanceId(),
                    EffectId = ccId,
                    TargetId = targetId,
                    SourceId = session.UserId,
                    StartTick = startTick,
                    Stacks = 1,
                    Amount = 0,
                });
            }
        }

        // 2) 몬스터 피격 → 서버 권위 HP 차감(GAS) → S_MonsterState / S_MonsterDead — 신규(⑤)
        ApplyAttackToMonsters(session, room, ability, attacker, recvMs);
    }

    /// <summary>
    /// 데미지 S_ApplyEffect 의 **단일 라벨**(AC-B 안B). 수치는 이 effect 가 아니라 `ability.BaseDamage` 가 정하고
    /// 서버가 Amount(권위 델타)로 실어 보낸다 — 클라는 healthOverride 로 그대로 적용한다.
    /// (폐기된 basic_attack_dmg/combo_*_dmg/monster_attack_dmg 대체)
    /// </summary>
    public const string AbilityDamageEffectId = "ability_damage";

    /// <summary>owner(시전 세션)에게 현재 권위 마나를 정정 전송(차감/거부 시점). 리젠은 보내지 않는다.</summary>
    private static Task SendManaAsync(Session session, PlayerState state, CancellationToken ct)
        => session.SendPacketAsync(new S_PlayerMana
        {
            UserId = state.UserId,
            Mana = state.Mana,
            MaxMana = state.MaxMana,
        }, ct);

    /// <summary>
    /// 어빌리티 데미지를 스탯으로 스케일해 Health 모디파이어로 만든다(2.4 + AC-B 안B). 순수 — 단위 테스트 대상.
    /// <b>데미지 출처 = <c>ability.BaseDamage</c></b>(effect 카탈로그 수치가 아니라) →
    /// `StatCombatMath.MeleeDamage(baseDamage, attackPower, defense)`. 몬스터→플레이어와 동일 산식.
    /// on-hit effect(CC/버프)는 여기 포함하지 않는다 — 별도 S_ApplyEffect 로 부여한다(역할 분리).
    /// </summary>
    public static List<GameplayAttributeModifier> BuildDamageMods(AbilityDef ability, int attackPower, int defense)
    {
        int finalDamage = StatCombatMath.MeleeDamage(ability.BaseDamage, attackPower, defense);
        return new List<GameplayAttributeModifier>
        {
            GameplayAttributeModifier.Create(EGameplayAttribute.Health, -finalDamage, EModifierType.Additive),
        };
    }

    /// <summary>
    /// 시전자 hitbox 와 겹치는 몬스터에 on-hit 효과를 GAS Health 모디파이어로 적용(서버 권위).
    /// 사망 시 S_MonsterDead, 생존 시 갱신된 HP 를 S_MonsterState 로 즉시 브로드캐스트.
    /// </summary>
    private static void ApplyAttackToMonsters(Session session, global::Server.Room.Room room, AbilityDef ability, PlayerState attacker, long recvMs)
    {
        // 스탯 기반 데미지(2.4 + AC-B 안B): **어빌리티 baseDamage** 를 base 로 attacker.AttackPower 로 스케일.
        // 몬스터 defense=0(몬스터 방어 스탯은 미도입).
        const int MonsterDefense = 0;
        var mods = BuildDamageMods(ability, attacker.AttackPower, MonsterDefense);

        var attackerPos = new Vector3(attacker.PosX, attacker.PosY, attacker.PosZ);
        bool anyKilled = false;

        foreach (var monster in room.GetAllMonsters())
        {
            if (monster.IsDead)
                continue;

            var targetPos = new Vector3(monster.PosX, monster.PosY, monster.PosZ);
            if (!HitboxMath.Overlaps(attackerPos, attacker.RotY, ability.Timeline.Hitbox, targetPos, TargetRadius))
                continue;

            int hpBefore = monster.Hp; // 차감 전 스냅샷 — 트레이스의 hp before→after 근거
            var (hit, newHp, dead) = room.DamageMonster(monster.InstanceId, mods);
            if (!hit)
                continue;

            int stateSeq = 0; // 사망이면 S_MonsterState 가 없어 상관키도 없다(0).
            if (dead)
            {
                anyKilled = true;
                room.Broadcast(new S_MonsterDead { InstanceId = monster.InstanceId });
                SpawnDrops(room, monster);
            }
            else
            {
                stateSeq = monster.NextSeq();
                room.Broadcast(new S_MonsterState
                {
                    InstanceId = monster.InstanceId,
                    PosX = monster.PosX, PosY = monster.PosY, PosZ = monster.PosZ,
                    RotY = monster.RotY,
                    Hp = newHp,
                    Phase = (byte)monster.Phase,
                    // AC-C3: 틱이 **먼저 만들어 둔 옛 HP 패킷**보다 이 패킷이 새 상태임을 클라에 알린다.
                    // 도착 순서가 뒤집혀도 클라가 Seq 로 스테일을 버린다(근본 해법).
                    Seq = stateSeq,
                });
                // ※ 여기서 MarkStateSent() 를 호출하지 않는다(AC-C3-hotfix).
                //   AC-C3(Seq) 로 클라가 스테일을 버리게 된 뒤에도 이 생략은 그대로 둔다:
                //   마킹하면 다음 틱이 재전송을 포기하는데, 그 상태에서 Seq 판정에 구멍이 생기면
                //   되돌릴 방법이 없다. 마킹을 생략하면 다음 틱이 무조건 재전송해 **자가 교정**된다
                //   = Seq(순서 무효화) + 재전송(자가 교정)의 이중 안전망. 비용은 피격당 1패킷뿐.
            }

            // 트레이스는 브로드캐스트 **뒤**에 남긴다 — 이 HP 를 실어 나른 패킷의 Seq 를 상관키로 싣기 위해.
            // (직전 Seq 를 찍으면 클라 로그와 조인이 어긋난다.)
            CombatTrace.Damage(
                CombatPath.PlayerToMonster, CombatTrace.FormulaMelee,
                ActorIds.FromPlayer(attacker.UserId), ActorIds.FromMonster(monster.InstanceId),
                ability.Id, ability.NetworkId,
                ability.BaseDamage, attacker.AttackPower, MonsterDefense,
                finalDamage: hpBefore - newHp,
                hpBefore, newHp,
                recvMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), stateSeq);
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
        // AC-E4/G: 레벨을 반영해 굴린다(수량 = 보상 감각). 등급별 확률은 **변종 자기 ID 의 드롭 테이블**이 갖는다.
        var drops = DropTableCatalog.Roll(monster.MonsterId, Random.Shared, monster.Level);
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
