using Script.System.GamePlayAbilitySystem;
using Server.Room;

namespace Server.Actors;

/// <summary>
/// 방 하나의 <b>액터·참가자 저장소</b>. 누가 존재하고 어떻게 찾는가를 소유한다.
///
/// <para><b>두 딕셔너리, 락 하나</b>: 액터는 <c>ActorId</c>(양수=UserId / 음수=−InstanceId)로,
/// 참가자는 <c>UserId</c> 로 찾는다. 둘은 항상 함께 바뀌므로(참가자가 들어오면 액터도 생긴다)
/// 같은 락으로 보호한다 — 나누면 예전처럼 락 중첩이 생긴다.</para>
///
/// <para><b>복합 연산은 <see cref="SyncRoot"/> 를 잡는다.</b> 한 틱 전체처럼 여러 연산이 하나의
/// 원자 단위여야 하는 경우, 호출자가 이 락을 잡고 그 안에서 이 클래스의 메서드를 부른다
/// (Monitor 는 재진입 가능하므로 안전하다). 그때 순회는 <see cref="ActorsLocked"/> 를 쓴다 — 스냅샷 할당이 없다.</para>
///
/// <para>이 저장소는 <b>패킷도 방 진행도 모른다</b>. 사망 후속(몬스터=제거·드랍 / 플레이어=다운 집계)은
/// 호출자가 <see cref="ActorKind"/> 로 분기한다 — HP 산정까지가 저장소의 일이다.</para>
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
    /// 참가자와 그 캐릭터를 만든다(게임 시작 시 1회, 소켓 입장 전).
    /// 스탯은 GameServer 가 계산해 보낸 권위값 — 여기선 받아서 GAS 에 부여만 한다.
    /// 플레이어는 네 속성을 <b>모두 보유</b>한다(0 이어도 보유 — "0 인 스탯"과 "스탯 없음"은 다르다).
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

    /// <summary>
    /// 이동 릴레이 — 클라 권위 위치를 그대로 반영한다(서버는 중계만). 미존재 참가자는 무동작(false).
    /// </summary>
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

    /// <summary>
    /// <b>효과 적용의 단일 진입점</b>(종족 무관). 모디파이어를 액터의 GAS 에 적용하고 결과를 돌려준다.
    /// 사망한 몬스터는 즉시 저장소에서 사라진다(플레이어는 다운 상태로 남아 부활 대상이 된다).
    /// </summary>
    /// <returns>Applied=대상 존재·생존이었는가, NewHp=적용 후 HP, DiedNow=이번 적용으로 사망했는가.</returns>
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

    /// <summary>몬스터 대상 <see cref="ApplyEffect"/> 축약(InstanceId 로 부른다). 반환 = (적중, 새 HP, 이번에 사망).</summary>
    public (bool Hit, int NewHp, bool Dead) DamageMonster(int instanceId, IReadOnlyList<GameplayAttributeModifier> mods)
        => ApplyEffect(ActorIds.FromMonster(instanceId), mods);
}
