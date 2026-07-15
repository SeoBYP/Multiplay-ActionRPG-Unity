using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;

namespace Server.Tests.Room;

/// <summary>
/// M4 B 트랙: 던전 실패(참가자 전원 다운) 집계 + 클리어/실패 상호 배타(Room._outcome).
/// TryMarkFailed 는 기대 로스터의 전원이 다운됐을 때만 최초 1회 true.
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
        return new global::Server.Room.Room(1, players, NullLogger<global::Server.Room.Room>.Instance);
    }

    private static void SpawnSlimes(global::Server.Room.Room room, int count)
        => room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, count, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

    [Fact]
    public void 일부만_다운이면_실패가_아니다()
    {
        var room = NewRoom(1, 2);

        Assert.False(room.TryMarkFailed(1)); // 2번은 아직 생존
    }

    [Fact]
    public void 참가자_전원_다운이면_TryMarkFailed는_최초_1회만_true다()
    {
        var room = NewRoom(1, 2);

        Assert.False(room.TryMarkFailed(1));
        Assert.True(room.TryMarkFailed(2));   // 전원 다운 → 최초 발화
        Assert.False(room.TryMarkFailed(2));  // 재호출 false
    }

    [Fact]
    public void 기대_로스터에_없는_userId_다운은_무시된다()
    {
        var room = NewRoom(1, 2);

        Assert.False(room.TryMarkFailed(999)); // 무관한 유저
        Assert.False(room.TryMarkFailed(1));   // 1만 다운 → 아직 실패 아님
    }

    [Fact]
    public void 같은_유저가_중복_다운보고해도_전원_집계는_한_번만_센다()
    {
        var room = NewRoom(1, 2);

        Assert.False(room.TryMarkFailed(1));
        Assert.False(room.TryMarkFailed(1)); // 중복 — 여전히 2번 미다운
        Assert.True(room.TryMarkFailed(2));  // 비로소 전원 다운
    }

    [Fact]
    public void 클리어가_먼저_발화되면_실패는_발화되지_않는다()
    {
        var room = NewRoom(1, 2);
        SpawnSlimes(room, 1);
        foreach (var m in room.GetAllMonsters())
            room.DamageMonster(m.InstanceId, new[] { Lethal });

        Assert.True(room.TryMarkCleared()); // 먼저 클리어 claim

        Assert.False(room.TryMarkFailed(1));
        Assert.False(room.TryMarkFailed(2)); // 전원 다운돼도 outcome 이미 Cleared → 실패 불가
    }

    [Fact]
    public void 실패가_먼저_발화되면_클리어는_발화되지_않는다()
    {
        var room = NewRoom(1, 2);
        SpawnSlimes(room, 1);

        Assert.False(room.TryMarkFailed(1));
        Assert.True(room.TryMarkFailed(2)); // 먼저 실패 claim

        // 그 뒤 몬스터를 전멸시켜도 클리어 불가(outcome 이미 Failed).
        foreach (var m in room.GetAllMonsters())
            room.DamageMonster(m.InstanceId, new[] { Lethal });
        Assert.False(room.TryMarkCleared());
    }
}
