using Script.System.GamePlayAbilitySystem;
using Server.Actors;
using Server.Room;

namespace Server.Tests.Room;

/// <summary>
/// 던전 진행 판정 — <b>방 없이</b> 저장소만으로 돌린다(Room 에서 떼어낸 덕).
///
/// <para>넷(클리어·실패·다운·부활)이 한 클래스인 이유는 <b>하나의 terminal 상태를 공유</b>하기 때문이다.
/// 여기서 고정하는 것도 그 원자성이다 — 클리어와 실패는 동시에 발화하지 못하고,
/// 실패 확정 뒤에는 부활이 막힌다.</para>
/// </summary>
public class DungeonProgressTests
{
    private static readonly GameplayAttributeModifier Lethal =
        new(EGameplayAttribute.Health, -9999, EModifierType.Additive);

    private static (DungeonProgress Progress, ActorStore Store) NewProgress(params long[] userIds)
    {
        var store = new ActorStore();
        foreach (var (id, i) in userIds.Select((id, i) => (id, i)))
        {
            var member = store.AddPlayer(id, $"P{id}", i, 0f, 0f, 0f, 0f,
                attackPower: 0, defense: 0, maxHp: 100, maxMana: 50);
            member.HasJoined = true;
        }
        return (new DungeonProgress(store), store);
    }

    private static void AddMonster(ActorStore store, DungeonProgress progress)
    {
        var monster = new MonsterActor(store.NextMonsterInstanceId()) { MonsterId = "creepy_demon" };
        monster.Gas.DefineResource(EGameplayAttribute.Health, 30);
        store.Add(monster);
        progress.MarkMonstersSpawned();
    }

    private static bool Kill(DungeonProgress progress, long userId)
        => progress.ApplyPlayerEffect(userId, new[] { Lethal }).FailClaimed;

    // ── 클리어 ──────────────────────────────────────────────────────────

    [Fact]
    public void 스폰한_적이_없으면_클리어가_아니다()
    {
        // 빈 방을 "몬스터 0 = 전멸"로 오판하면 입장하자마자 클리어가 난다.
        var (progress, _) = NewProgress(1);

        Assert.False(progress.TryMarkCleared());
    }

    [Fact]
    public void 몬스터가_남아있으면_클리어가_아니다()
    {
        var (progress, store) = NewProgress(1);
        AddMonster(store, progress);

        Assert.False(progress.TryMarkCleared());
    }

    [Fact]
    public void 전멸하면_클리어는_최초_1회만_claim된다()
    {
        var (progress, store) = NewProgress(1);
        AddMonster(store, progress);
        store.ApplyEffect(ActorIds.FromMonster(1), new[] { Lethal }); // 사망 → 저장소에서 제거

        Assert.True(progress.TryMarkCleared());
        Assert.False(progress.TryMarkCleared()); // 재호출 false
    }

    [Fact]
    public void 동시에_클리어를_시도해도_한_번만_통과한다()
    {
        var (progress, store) = NewProgress(1);
        AddMonster(store, progress);
        store.ApplyEffect(ActorIds.FromMonster(1), new[] { Lethal });

        int claimed = 0;
        Parallel.For(0, 64, _ =>
        {
            if (progress.TryMarkCleared()) Interlocked.Increment(ref claimed);
        });

        Assert.Equal(1, claimed);
    }

    // ── 다운·실패 ───────────────────────────────────────────────────────

    [Fact]
    public void 만피인_참가자의_자기신고_다운은_거부된다()
    {
        // C_PlayerDead 는 클라 예측 통지일 뿐 — 서버 HP 가 살아 있으면 다운시키지 않는다.
        var (progress, store) = NewProgress(1);

        var (newly, failed) = progress.MarkDowned(1);

        Assert.False(newly);
        Assert.False(failed);
        Assert.False(store.GetMember(1)!.Actor.Gas.HasTag(GameplayTags.Dead));
    }

    [Fact]
    public void 참가자가_아닌_userId는_무시된다()
    {
        var (progress, _) = NewProgress(1);

        Assert.False(progress.MarkDowned(999).NewlyDowned);
    }

    [Fact]
    public void 일부만_다운이면_실패가_아니다()
    {
        var (progress, _) = NewProgress(1, 2);

        Assert.False(Kill(progress, 1));
    }

    [Fact]
    public void 전원_다운이면_실패는_최초_1회만_claim된다()
    {
        var (progress, _) = NewProgress(1, 2);

        Assert.False(Kill(progress, 1));
        Assert.True(Kill(progress, 2));
        Assert.False(progress.TryMarkFailed(2)); // 재호출 false
        Assert.True(progress.IsFailed);
    }

    [Fact]
    public void 다운은_태그로_dedup된다()
    {
        var (progress, _) = NewProgress(1, 2);

        Assert.True(progress.ApplyPlayerEffect(1, new[] { Lethal }).NewlyDowned);
        Assert.False(progress.MarkDowned(1).NewlyDowned); // 이미 붙어 있다
    }

    // ── 상호 배타 ───────────────────────────────────────────────────────

    [Fact]
    public void 클리어가_먼저면_실패가_발화되지_않는다()
    {
        var (progress, store) = NewProgress(1, 2);
        AddMonster(store, progress);
        store.ApplyEffect(ActorIds.FromMonster(1), new[] { Lethal });
        Assert.True(progress.TryMarkCleared());

        Assert.False(Kill(progress, 1));
        Assert.False(Kill(progress, 2)); // 전원 다운돼도 이미 Cleared
        Assert.False(progress.IsFailed);
    }

    [Fact]
    public void 실패가_먼저면_클리어가_발화되지_않는다()
    {
        var (progress, store) = NewProgress(1, 2);
        AddMonster(store, progress);

        Assert.False(Kill(progress, 1));
        Assert.True(Kill(progress, 2));

        store.ApplyEffect(ActorIds.FromMonster(1), new[] { Lethal }); // 전멸시켜도
        Assert.False(progress.TryMarkCleared());                      // 이미 Failed
    }

    // ── 부활 ────────────────────────────────────────────────────────────

    [Fact]
    public void 다운된_아군을_사거리_안에서_부활시킨다()
    {
        var (progress, store) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(2, new[] { Lethal });

        var (ok, hp) = progress.TryRevive(1, 2);

        Assert.True(ok);
        Assert.Equal(100 * ReviveConfig.RestorePercent / 100, hp);
        Assert.False(store.GetMember(2)!.Actor.Gas.HasTag(GameplayTags.Dead));
        Assert.False(store.GetMember(2)!.Actor.Gas.IsDead);
    }

    [Fact]
    public void 부활은_멱등이다()
    {
        var (progress, _) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(2, new[] { Lethal });

        Assert.True(progress.TryRevive(1, 2).Ok);
        Assert.False(progress.TryRevive(1, 2).Ok); // 이미 부활 — 태그가 없다
    }

    [Fact]
    public void 동시_부활은_한_번만_성공한다()
    {
        var (progress, _) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(2, new[] { Lethal });

        int ok = 0;
        Parallel.For(0, 64, _ =>
        {
            if (progress.TryRevive(1, 2).Ok) Interlocked.Increment(ref ok);
        });

        Assert.Equal(1, ok);
    }

    [Fact]
    public void 사거리_밖이면_부활이_거부된다()
    {
        var (progress, store) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(2, new[] { Lethal });
        store.GetMember(2)!.Actor.PosX = ReviveConfig.RangeMeters + 1f;

        Assert.False(progress.TryRevive(1, 2).Ok);
    }

    [Fact]
    public void 자기_자신은_부활_대상이_아니다()
    {
        var (progress, _) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(2, new[] { Lethal });

        Assert.False(progress.TryRevive(2, 2).Ok);
    }

    [Fact]
    public void 다운되지_않은_대상은_부활_대상이_아니다()
    {
        var (progress, _) = NewProgress(1, 2);

        Assert.False(progress.TryRevive(1, 2).Ok);
    }

    [Fact]
    public void 시전자가_다운이면_부활할_수_없다()
    {
        var (progress, _) = NewProgress(1, 2);
        progress.ApplyPlayerEffect(1, new[] { Lethal }); // 시전자도 다운
        progress.ApplyPlayerEffect(2, new[] { Lethal });

        Assert.False(progress.TryRevive(1, 2).Ok);
    }

    [Fact]
    public void 실패_확정_뒤에는_부활이_막힌다()
    {
        // 전원 다운으로 던전이 끝났는데 되살아나면 결과가 뒤집힌다.
        var (progress, _) = NewProgress(1, 2);
        Kill(progress, 1);
        Assert.True(Kill(progress, 2)); // 실패 claim

        Assert.False(progress.TryRevive(1, 2).Ok);
    }
}
