using Script.System.GamePlayAbilitySystem;
using Server.Actors;

namespace Server.Room;

// 클리어/실패는 Interlocked.CompareExchange 로 최초 1회만 claim(상호 배타). 다운은 Dead 태그가 곧 dedup/멱등 가드.
public sealed class DungeonProgress
{
    // 0=진행 중 · 1=클리어 · 2=실패
    private const int OutcomeNone = 0;
    private const int OutcomeCleared = 1;
    private const int OutcomeFailed = 2;

    private readonly ActorStore _actors;
    private int _outcome;

    // 빈 방을 "전멸=클리어"로 오판하지 않기 위한 가드.
    private bool _monstersSpawned;

    public DungeonProgress(ActorStore actors) => _actors = actors;

    public void MarkMonstersSpawned() => _monstersSpawned = true;

    public bool IsFailed => System.Threading.Volatile.Read(ref _outcome) == OutcomeFailed;

    // 사망 몬스터는 ActorStore.ApplyEffect 가 즉시 제거하므로 "몬스터 0 == 전멸".
    public bool TryMarkCleared()
    {
        if (!_monstersSpawned || _actors.HasAnyMonster())
            return false;

        return System.Threading.Interlocked.CompareExchange(ref _outcome, OutcomeCleared, OutcomeNone) == OutcomeNone;
    }

    // 서버 HP 가 실제로 0 이어야 한다 — 클라 C_PlayerDead 자기신고로 만피 다운해 몬스터 타깃에서 빠지는 것을 막는다.
    public (bool NewlyDowned, bool FailClaimed) MarkDowned(long userId)
    {
        bool newly;
        bool allDown;
        lock (_actors.SyncRoot)
        {
            var member = _actors.GetMember(userId);
            if (member is null)
                return (false, false);

            if (!member.Actor.Gas.IsDead)
                return (false, false);

            newly = member.Actor.Gas.AddTag(GameplayTags.Dead);
            allDown = _actors.MemberCount > 0
                      && _actors.MembersLocked.All(m => m.Actor.Gas.HasTag(GameplayTags.Dead));
        }

        bool failClaimed = allDown
            && System.Threading.Interlocked.CompareExchange(ref _outcome, OutcomeFailed, OutcomeNone) == OutcomeNone;
        return (newly, failClaimed);
    }

    public bool TryMarkFailed(long userId) => MarkDowned(userId).FailClaimed;

    // HP 0 순간을 놓치지 않으려면 ApplyEffect + MarkDowned 가 한 호출이어야 한다.
    public (int NewHp, bool NewlyDowned, bool FailClaimed) ApplyPlayerEffect(
        long userId, IReadOnlyList<GameplayAttributeModifier> mods)
    {
        var (applied, newHp, _) = _actors.ApplyEffect(ActorIds.FromPlayer(userId), mods);
        if (!applied)
            return (0, false, false);
        if (newHp > 0)
            return (newHp, false, false);

        var (newly, failClaimed) = MarkDowned(userId);
        return (newHp, newly, failClaimed);
    }

    public (bool Ok, int NewHp) TryRevive(long reviverId, long targetId)
    {
        if (reviverId == targetId)
            return (false, 0);
        if (IsFailed)
            return (false, 0);

        lock (_actors.SyncRoot)
        {
            var reviver = _actors.GetMember(reviverId);
            if (reviver is null
                || !reviver.HasJoined || reviver.Actor.Gas.IsDead || reviver.DisconnectedAtMs is not null)
                return (false, 0);

            var target = _actors.GetMember(targetId);
            if (target is null)
                return (false, 0);

            float dx = reviver.Actor.PosX - target.Actor.PosX;
            float dz = reviver.Actor.PosZ - target.Actor.PosZ;
            if (dx * dx + dz * dz > ReviveConfig.RangeMeters * ReviveConfig.RangeMeters)
                return (false, 0);

            // 태그 제거가 멱등 가드(중복 C_Revive 차단).
            if (!target.Actor.Gas.RemoveTag(GameplayTags.Dead))
                return (false, 0);

            int hp = Math.Max(1, target.Actor.Gas.Max(EGameplayAttribute.Health) * ReviveConfig.RestorePercent / 100);
            target.Actor.Gas[EGameplayAttribute.Health] = hp;
            return (true, hp);
        }
    }
}
