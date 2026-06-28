using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Combat;

/// <summary>
/// 2.5.2 Co-op 부활 — 서버 권위 검증(거리·다운상태·미실패) + HP 부분복구 + 멱등.
/// </summary>
public class ReviveTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo>
            {
                new() { UserId = 100, Nickname = "R", SpawnIndex = 0 },
                new() { UserId = 200, Nickname = "T", SpawnIndex = 1 },
            },
            NullLogger<global::Server.Room.Room>.Instance);

    private static GameplayAttributeModifier[] Lethal()
        => new[] { GameplayAttributeModifier.Create(EGameplayAttribute.Health, -9999, EModifierType.Additive) };

    /// <summary>시전자(100) 생존·근접, 대상(200) 다운 상태로 만든 방.</summary>
    private static global::Server.Room.Room DownedTargetRoom(float targetX = 1f)
    {
        var room = NewRoom();
        room.InitPlayerState(100, "R", 0, 0f, 0f, 0f, 0f, maxHealth: 200); // 시전자
        room.InitPlayerState(200, "T", 1, targetX, 0f, 0f, 0f, maxHealth: 200); // 대상
        room.MarkJoined(100);
        room.MarkJoined(200);
        room.ApplyPlayerEffect(200, Lethal()); // 대상 HP0 → 다운
        return room;
    }

    [Fact]
    public void 부활은_다운된_아군의_HP를_50퍼센트_복구하고_다운에서_제거한다()
    {
        var room = DownedTargetRoom();
        Assert.True(room.GetPlayerState(200)!.IsDowned);

        var (ok, hp) = room.TryRevive(100, 200);

        Assert.True(ok);
        Assert.Equal(100, hp); // MaxHp 200 × 50%
        Assert.Equal(100, room.GetPlayerState(200)!.Hp);
        Assert.False(room.GetPlayerState(200)!.IsDowned);
    }

    [Fact]
    public void 거리가_사거리_밖이면_부활은_거부된다()
    {
        var room = DownedTargetRoom(targetX: 100f); // ReviveConfig.RangeMeters(2.5) 밖

        var (ok, _) = room.TryRevive(100, 200);

        Assert.False(ok);
        Assert.True(room.GetPlayerState(200)!.IsDowned); // 여전히 다운
    }

    [Fact]
    public void 다운되지_않은_대상이거나_이미_부활됐으면_거부된다_멱등()
    {
        var room = DownedTargetRoom();

        var first = room.TryRevive(100, 200);
        Assert.True(first.Ok);

        // 두 번째 — 이미 부활(다운 아님) → 멱등 거부.
        var second = room.TryRevive(100, 200);
        Assert.False(second.Ok);
    }

    [Fact]
    public void 자기_자신은_부활_대상이_될_수_없다()
    {
        var room = DownedTargetRoom();
        Assert.False(room.TryRevive(200, 200).Ok); // reviver==target
    }
}
