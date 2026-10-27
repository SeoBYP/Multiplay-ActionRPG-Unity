using Script.System.GamePlayAbilitySystem;
using Server.Room;

namespace Server.Actors;

/// <summary>
/// 방 하나의 액터·참가자 저장소. 액터(ActorId)와 참가자(UserId)는 항상 함께 바뀌므로 락 하나로 보호한다.
/// 복합 연산은 호출자가 <see cref="SyncRoot"/> 를 잡고(재진입 가능) 그 안에서 <see cref="ActorsLocked"/> 로 순회한다.
/// 패킷·방 진행은 모른다 — 사망 후속은 호출자가 <see cref="ActorKind"/> 로 분기한다.
/// </summary>
public sealed class ActorStore
{
    /// <summary>액터(플레이어 + 몬스터). 키 = ActorId.</summary>
    private readonly Dictionary<long, Actor> _actors = new();

    /// <summary>방 참가자. 키 = UserId.</summary>
    private readonly Dictionary<long, RoomMember> _members = new();

    private int _nextMonsterInstanceId;

    /// <summary>복합 연산을 원자적으로 묶을 때 호출자가 잡는 락. 단일 연산은 이 클래스가 알아서 잡는다.</summary>
    public object SyncRoot { get; } = new();

    // ── 참가자(플레이어) ────────────────────────────────────────────────

    /// <summary>
    /// 게임 시작 시 1회(소켓 입장 전) 참가자와 액터를 만든다. 스탯은 GameServer 권위값.
    /// 네 속성을 0 이어도 모두 부여한다 — "0 인 스탯"과 "스탯 없음"은 다르다.
    /// </summary>
    public RoomMember AddPlayer(
        long userId, string nickname, int spawnIndex,
        float x, float y, float z, float rotY,
        int attackPower, int defense, int maxHp, int maxMana)
    {
        var actor = new PlayerActor(userId) { PosX = x, PosY = y, PosZ = z, RotY = rotY };
        actor.Gas.DefineResource(EGameplayAttribute.Health, maxHp);
        actor.Gas.DefineResource(EGameplayAttribute.Mana, maxMana);
        actor.Gas.DefineStat(EGameplayAttribute.AttackPower, attackPower);
        actor.Gas.DefineStat(EGameplayAttribute.Defense, defense);

        var member = new RoomMember
        {
            UserId = userId,
            Nickname = nickname,
            SpawnIndex = spawnIndex,
            Actor = actor,
        };

        lock (SyncRoot)
        {
            _members[userId] = member;
            _actors[actor.ActorId] = actor;
        }

        return member;
    }

    public RoomMember? GetMember(long userId)
    {
        lock (SyncRoot) return _members.GetValueOrDefault(userId);
    }

    /// <summary>참가자 스냅샷(락 밖에서 순회해도 안전한 복사본).</summary>
    public IReadOnlyList<RoomMember> Members()
    {
        lock (SyncRoot) return _members.Values.ToList();
    }

    /// <summary>참가자와 그 액터를 함께 제거한다. 반환 = 실제로 있었는가.</summary>
    public bool RemoveMember(long userId)
    {
        lock (SyncRoot)
        {
            if (!_members.Remove(userId, out var member))
                return false;
            _actors.Remove(member.Actor.ActorId);
            return true;
        }
    }

    /// <summary>클라가 보낸 위치를 그대로 반영한다. 미존재 참가자는 false.</summary>
    public bool SetPosition(long userId, float x, float y, float z, float rotY)
    {
        lock (SyncRoot)
        {
            if (!_members.TryGetValue(userId, out var member))
                return false;

            member.Actor.PosX = x;
            member.Actor.PosY = y;
            member.Actor.PosZ = z;
            member.Actor.RotY = rotY;
            return true;
        }
    }

    public int MemberCount
    {
        get { lock (SyncRoot) return _members.Count; }
    }

    // ── 몬스터 ──────────────────────────────────────────────────────────

    /// <summary>방 단위 순차 InstanceId 발급(1부터). 스폰 시에만 부른다.</summary>
    public int NextMonsterInstanceId()
    {
        lock (SyncRoot) return ++_nextMonsterInstanceId;
    }

    public void Add(MonsterActor monster)
    {
        lock (SyncRoot) _actors[monster.ActorId] = monster;
    }

    public MonsterActor? GetMonster(int instanceId)
    {
        lock (SyncRoot) return _actors.GetValueOrDefault(ActorIds.FromMonster(instanceId)) as MonsterActor;
    }

    /// <summary>몬스터 스냅샷.</summary>
    public IReadOnlyList<MonsterActor> Monsters()
    {
        lock (SyncRoot) return _actors.Values.OfType<MonsterActor>().ToList();
    }

    public bool RemoveMonster(int instanceId)
    {
        lock (SyncRoot) return _actors.Remove(ActorIds.FromMonster(instanceId));
    }

    /// <summary>살아 있는 몬스터가 하나라도 있는가(클리어 판정). 스냅샷 할당 없이 센다.</summary>
    public bool HasAnyMonster()
    {
        lock (SyncRoot)
        {
            foreach (var actor in _actors.Values)
                if (actor.Kind == ActorKind.Monster)
                    return true;
            return false;
        }
    }

    // ── 락 안 순회 (틱 전용) ────────────────────────────────────────────

    /// <summary>액터 전체. <b>호출자가 <see cref="SyncRoot"/> 를 잡고 있어야 한다.</b> 스냅샷 할당이 없다.</summary>
    public IEnumerable<Actor> ActorsLocked => _actors.Values;

    /// <summary>참가자 전체. <b>호출자가 <see cref="SyncRoot"/> 를 잡고 있어야 한다.</b></summary>
    public IEnumerable<RoomMember> MembersLocked => _members.Values;

    // ── 효과 적용 ───────────────────────────────────────────────────────

    /// <summary>효과 적용의 단일 진입점(종족 무관). 사망한 몬스터는 즉시 제거, 플레이어는 다운으로 남는다(부활 대상).</summary>
    /// <returns>Applied=대상이 존재·생존했는가, DiedNow=이번 적용으로 사망했는가.</returns>
    public (bool Applied, int NewHp, bool DiedNow) ApplyEffect(
        long actorId, IReadOnlyList<GameplayAttributeModifier> mods)
    {
        lock (SyncRoot)
        {
            if (!_actors.TryGetValue(actorId, out var actor) || actor.Gas.IsDead)
                return (false, 0, false);

            actor.Gas.ApplyModifiers(mods);
            bool died = actor.Gas.IsDead;

            if (died && actor.Kind == ActorKind.Monster)
                _actors.Remove(actorId);

            return (true, actor.Gas[EGameplayAttribute.Health], died);
        }
    }

    /// <summary>몬스터 대상 <see cref="ApplyEffect"/> 축약(InstanceId 로 부른다).</summary>
    public (bool Hit, int NewHp, bool Dead) DamageMonster(int instanceId, IReadOnlyList<GameplayAttributeModifier> mods)
        => ApplyEffect(ActorIds.FromMonster(instanceId), mods);
}
