using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// 던전 실패(참가자 전원 다운) 집계 + 클리어/실패 상호 배타(Room._outcome).
///
/// <para><b>다운은 액터의 상태다</b> — 예전엔 별도 <c>_downed</c> HashSet 이 진실원이라
/// 참가자를 만들지 않고 <c>TryMarkFailed(id)</c> 만 불러도 다운으로 집계됐다.
/// 지금은 <b>서버 HP 가 0 이어야</b> <see cref="GameplayTags.Dead"/> 태그가 붙고, 그 태그가 곧 집계 근거다.
/// 그래서 이 테스트들은 실제로 플레이어를 만들고 실제로 죽인다.</para>
/// </summary>
public class DungeonFailTests
{
    private static readonly GameplayAttributeModifier Lethal =
        new(EGameplayAttribute.Health, -999, EModifierType.Additive);

    private static global::Server.Room.Room NewRoom(params long[] userIds)
    {
        var players = userIds
            .Select((id, i) => new PlayerInfo { UserId = id, Nickname = $"P{id}", SpawnIndex = i })
            .ToList();
        var room = new global::Server.Room.Room(1, players, NullLogger<global::Server.Room.Room>.Instance);
        foreach (var p in players)
            room.AddPlayer(p.UserId, p.Nickname, p.SpawnIndex, 0f, 0f, 0f, 0f);
        return room;
    }

    /// <summary>실제로 HP 를 0 으로 만든다. 반환 = 이 죽음으로 실패가 claim 됐는가.</summary>
    private static bool Kill(global::Server.Room.Room room, long userId)
        => room.Progress.ApplyPlayerEffect(userId, new[] { Lethal }).FailClaimed;

    private static void SpawnSlimes(global::Server.Room.Room room, int count)
        => room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, count, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

    [Fact]
    public void 일부만_다운이면_실패가_아니다()
    {
        var room = NewRoom(1, 2);

        Assert.False(Kill(room, 1)); // 2번은 아직 생존
    }

    [Fact]
    public void 참가자_전원_다운이면_실패는_최초_1회만_claim된다()
    {
        var room = NewRoom(1, 2);

        Assert.False(Kill(room, 1));
        Assert.True(Kill(room, 2));           // 전원 다운 → 최초 발화
        Assert.False(room.Progress.TryMarkFailed(2));  // 재호출 false
    }

    [Fact]
    public void 참가자가_아닌_userId_다운보고는_무시된다()
    {
        var room = NewRoom(1, 2);

        Assert.False(room.Progress.TryMarkFailed(999)); // 이 방의 참가자가 아님
        Assert.False(Kill(room, 1));           // 1만 다운 → 아직 실패 아님
    }

    [Fact]
    public void 같은_유저가_중복_다운보고해도_전원_집계는_한_번만_센다()
    {
        var room = NewRoom(1, 2);

        Assert.False(Kill(room, 1));
        Assert.False(room.Progress.TryMarkFailed(1)); // 중복 — 여전히 2번 미다운
        Assert.True(Kill(room, 2));          // 비로소 전원 다운
    }

    [Fact]
    public void 만피인_플레이어의_자기신고_다운은_거부된다()
    {
        // C_PlayerDead 는 클라 예측 통지일 뿐이다. 서버 HP 가 살아 있으면 다운시키지 않는다 —
        // 만피인 채로 다운을 신고해 몬스터 AI 타깃에서 빠지는(=사실상 무적) 구멍을 막는다.
        var room = NewRoom(1, 2);

        var (newlyDowned, failClaimed) = room.Progress.MarkDowned(1);

        Assert.False(newlyDowned);
        Assert.False(failClaimed);
        Assert.False(room.Actors.GetMember(1)!.Actor.Gas.HasTag(GameplayTags.Dead));

        // 둘 다 신고해도 실패가 발화되지 않는다.
        room.Progress.MarkDowned(2);
        Assert.False(room.Progress.MarkDowned(1).FailClaimed);
    }

    [Fact]
    public void 클리어가_먼저_발화되면_실패는_발화되지_않는다()
    {
        var room = NewRoom(1, 2);
        SpawnSlimes(room, 1);
        foreach (var m in room.Actors.Monsters())
            room.Actors.DamageMonster(m.InstanceId, new[] { Lethal });

        Assert.True(room.Progress.TryMarkCleared()); // 먼저 클리어 claim

        Assert.False(Kill(room, 1));
        Assert.False(Kill(room, 2)); // 전원 다운돼도 outcome 이미 Cleared → 실패 불가
    }

    [Fact]
    public void 실패가_먼저_발화되면_클리어는_발화되지_않는다()
    {
        var room = NewRoom(1, 2);
        SpawnSlimes(room, 1);

        Assert.False(Kill(room, 1));
        Assert.True(Kill(room, 2)); // 먼저 실패 claim

        // 그 뒤 몬스터를 전멸시켜도 클리어 불가(outcome 이미 Failed).
        foreach (var m in room.Actors.Monsters())
            room.Actors.DamageMonster(m.InstanceId, new[] { Lethal });
        Assert.False(room.Progress.TryMarkCleared());
    }
}
