using Script.System.GamePlayAbilitySystem;
using Server.Room;

namespace Server.Actors;

// 락 하나(SyncRoot)로 액터·참가자를 함께 보호. 복합 연산은 호출자가 락을 잡고(재진입 가능) ActorsLocked 로 순회.
public sealed class ActorStore
{
    private readonly Dictionary<long, Actor> _actors = new();

    private readonly Dictionary<long, RoomMember> _members = new();

    private int _nextMonsterInstanceId;

    public object SyncRoot { get; } = new();

    // 네 속성을 0 이어도 모두 부여한다 — "0 인 스탯"과 "스탯 없음"은 다르다.
    public RoomMember AddPlayer(
        long userId, string nickname, int spawnIndex,
        float x, float y, float z, float rotY,
        int attackPower, int defense, int maxHp, int maxMana, IReadOnlyList<string>? abilityIds = null)
    {
        var actor = new PlayerActor(userId) { PosX = x, PosY = y, PosZ = z, RotY = rotY };
        actor.Gas.DefineResource(EGameplayAttribute.Health, maxHp);
        actor.Gas.DefineResource(EGameplayAttribute.Mana, maxMana);
        actor.Gas.DefineStat(EGameplayAttribute.AttackPower, attackPower);
        actor.Gas.DefineStat(EGameplayAttribute.Defense, defense);

        GrantPlayerAbilities(PlayerActor.BasicAbilityIds, actor);
        GrantPlayerAbilities(abilityIds, actor);

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

    private void GrantPlayerAbilities(IReadOnlyList<string>? abilityIds, PlayerActor actor)
    {
        if (abilityIds is null) return;
        if (abilityIds.Count == 0) return;
        foreach (var abilityId in abilityIds) 
            actor.Gas.GrantAbility(abilityId);
    }

    public RoomMember? GetMember(long userId)
    {
        lock (SyncRoot) return _members.GetValueOrDefault(userId);
    }

    public IReadOnlyList<RoomMember> Members()
    {
        lock (SyncRoot) return _members.Values.ToList();
    }

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

    // 클라 권위 위치를 그대로 반영.
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

    public IReadOnlyList<MonsterActor> Monsters()
    {
        lock (SyncRoot) return _actors.Values.OfType<MonsterActor>().ToList();
    }

    public bool RemoveMonster(int instanceId)
    {
        lock (SyncRoot) return _actors.Remove(ActorIds.FromMonster(instanceId));
    }

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

    // 호출자가 SyncRoot 를 잡아야 한다. 스냅샷 할당 없음.
    public IEnumerable<Actor> ActorsLocked => _actors.Values;

    public IEnumerable<RoomMember> MembersLocked => _members.Values;

    // 단일 진입점. 몬스터는 사망 즉시 제거, 플레이어는 다운으로 남는다(부활 대상).
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

    public (bool Hit, int NewHp, bool Dead) DamageMonster(int instanceId, IReadOnlyList<GameplayAttributeModifier> mods)
        => ApplyEffect(ActorIds.FromMonster(instanceId), mods);
}
