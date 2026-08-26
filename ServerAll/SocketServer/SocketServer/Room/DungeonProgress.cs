using Script.System.GamePlayAbilitySystem;
using Server.Actors;

namespace Server.Room;

/// <summary>
/// 던전 한 판의 <b>진행 판정</b> — 클리어 · 실패 · 다운 · 부활.
///
/// <para>네 가지가 같은 클래스인 이유는 <b>하나의 terminal 상태를 공유</b>하기 때문이다.
/// 클리어와 실패는 <c>Interlocked.CompareExchange</c> 로 최초 1회만 claim 되고(상호 배타),
/// 부활은 "아직 실패가 아님"을 전제로만 성립한다. 흩어 놓으면 그 원자성을 지킬 주체가 사라진다.</para>
///
/// <para><b>다운은 액터의 태그다</b>(<see cref="GameplayTags.Dead"/>) — 별도 집합을 두지 않는다.
/// 태그의 Add/Remove 반환값이 그대로 dedup(사망 통지 1회)·멱등(중복 부활 차단) 가드가 된다.</para>
///
/// <para>액터 상태는 <see cref="ActorStore"/> 가 소유하고 여기선 <b>읽고 판정</b>만 한다.
/// 패킷은 모른다 — 무엇을 브로드캐스트할지는 호출자(핸들러/Room)가 정한다.</para>
/// </summary>
public sealed class DungeonProgress
{
    /// <summary>0=진행 중 · 1=클리어 · 2=실패. 단일 terminal — 최초 1회만 claim 된다.</summary>
    private const int OutcomeNone = 0;
    private const int OutcomeCleared = 1;
    private const int OutcomeFailed = 2;

    private readonly ActorStore _actors;
    private int _outcome;

    /// <summary>한 번이라도 몬스터가 스폰됐는가. 빈 방을 "전멸=클리어"로 오판하지 않기 위한 가드.</summary>
    private bool _monstersSpawned;

    public DungeonProgress(ActorStore actors) => _actors = actors;

    /// <summary>스폰이 일어났음을 알린다(클리어 오판 방지 가드를 켠다).</summary>
    public void MarkMonstersSpawned() => _monstersSpawned = true;

    /// <summary>실패가 확정됐는가(부활 차단 판정용).</summary>
    public bool IsFailed => System.Threading.Volatile.Read(ref _outcome) == OutcomeFailed;

    /// <summary>
    /// 몬스터가 전멸했으면 클리어를 <b>최초 1회만</b> claim 한다.
    /// 사망 몬스터는 <see cref="ActorStore.ApplyEffect"/> 가 즉시 제거하므로 "몬스터 0 == 전멸"이다.
    /// 동시 호출돼도 한 번만 true.
    /// </summary>
    public bool TryMarkCleared()
    {
        if (!_monstersSpawned || _actors.HasAnyMonster())
            return false;

        // 전멸 확인 후 terminal 을 원자적으로 claim — 실패와 동시 발화 불가.
        return System.Threading.Interlocked.CompareExchange(ref _outcome, OutcomeCleared, OutcomeNone) == OutcomeNone;
    }

    /// <summary>
    /// 한 참가자의 다운을 확정한다 — 액터에 <see cref="GameplayTags.Dead"/> 태그를 붙인다. 반환:
    ///   NewlyDowned = 이 호출로 처음 다운됐는가(태그가 곧 dedup — S_PlayerDead 1회 발화용).
    ///   FailClaimed = 이 다운으로 <b>전원</b> 다운이 돼 실패를 최초 claim 했는가(클리어와 상호 배타).
    ///
    /// <para><b>HP 가 실제로 0 이어야 한다.</b> 클라의 <c>C_PlayerDead</c> 는 예측 통지일 뿐이고,
    /// 서버 HP 가 살아 있으면 거부한다 — 만피인 채로 다운을 자기신고해 몬스터 AI 타깃에서 빠지는 것을 막는다.
    /// (HP 를 서버 권위로 올려놓고 다운 여부만 클라를 믿으면 불사 핵을 반만 막은 것이다.)</para>
    /// </summary>
    public (bool NewlyDowned, bool FailClaimed) MarkDowned(long userId)
    {
        bool newly;
        bool allDown;
        lock (_actors.SyncRoot)
        {
            var member = _actors.GetMember(userId);
            if (member is null)
                return (false, false); // 이 방의 참가자가 아님

            if (!member.Actor.Gas.IsDead)
                return (false, false); // 서버 HP 가 살아 있다 — 자기신고 거부

            newly = member.Actor.Gas.AddTag(GameplayTags.Dead);
            allDown = _actors.MemberCount > 0
                      && _actors.MembersLocked.All(m => m.Actor.Gas.HasTag(GameplayTags.Dead));
        }

        bool failClaimed = allDown
            && System.Threading.Interlocked.CompareExchange(ref _outcome, OutcomeFailed, OutcomeNone) == OutcomeNone;
        return (newly, failClaimed);
    }

    /// <summary>하위호환: 전원 다운 시 실패 claim 여부만 반환.</summary>
    public bool TryMarkFailed(long userId) => MarkDowned(userId).FailClaimed;

    /// <summary>
    /// 참가자에게 효과를 적용하고 <b>다운까지 집계</b>한다(<see cref="ActorStore.ApplyEffect"/> + <see cref="MarkDowned"/>).
    /// HP 가 0 이 되는 순간을 놓치지 않으려면 이 둘이 한 호출이어야 한다.
    /// 반환: (적용 후 HP, 이번에 처음 다운, 전원다운 실패 claim). 미존재 참가자는 (0,false,false).
    /// </summary>
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

    /// <summary>
    /// Co-op 부활(서버 권위). 검증: ① 자기 자신 아님 ② 던전 미실패 ③ 시전자 생존·입장·미끊김
    /// ④ 대상 다운 상태 ⑤ 평면 거리 ≤ <see cref="ReviveConfig.RangeMeters"/>.
    /// 통과 시 <see cref="GameplayTags.Dead"/> 태그를 떼고 HP 를 <see cref="ReviveConfig.RestorePercent"/>% 로 복구.
    /// 반환: (성공, 복구된 HP). 멱등 — 이미 부활/미다운이면 (false,0).
    /// </summary>
    public (bool Ok, int NewHp) TryRevive(long reviverId, long targetId)
    {
        if (reviverId == targetId)
            return (false, 0);
        if (IsFailed) // 전원 다운(실패) 확정 후엔 부활 불가
            return (false, 0);

        lock (_actors.SyncRoot)
        {
            var reviver = _actors.GetMember(reviverId);
            if (reviver is null
                || !reviver.HasJoined || reviver.Actor.Gas.IsDead || reviver.DisconnectedAtMs is not null)
                return (false, 0); // 시전자 미입장/다운/끊김 → 부활 불가

            var target = _actors.GetMember(targetId);
            if (target is null)
                return (false, 0);

            float dx = reviver.Actor.PosX - target.Actor.PosX;
            float dz = reviver.Actor.PosZ - target.Actor.PosZ;
            if (dx * dx + dz * dz > ReviveConfig.RangeMeters * ReviveConfig.RangeMeters)
                return (false, 0); // 거리 밖

            // 대상이 실제 다운이어야 부활. 태그 제거가 곧 멱등 가드(중복 C_Revive 차단).
            if (!target.Actor.Gas.RemoveTag(GameplayTags.Dead))
                return (false, 0);

            int hp = Math.Max(1, target.Actor.Gas.Max(EGameplayAttribute.Health) * ReviveConfig.RestorePercent / 100);
            target.Actor.Gas[EGameplayAttribute.Health] = hp;
            return (true, hp);
        }
    }
}
