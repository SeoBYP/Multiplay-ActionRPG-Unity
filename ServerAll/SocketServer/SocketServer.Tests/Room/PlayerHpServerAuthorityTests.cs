using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Room;

/// <summary>
/// 플레이어 HP 서버 권위(authority-model §4, 2026-06-11). 서버가 데미지/회복을 자기 HP 에 누적하고
/// HP≤0 을 **클라 C_PlayerDead 없이 직접 감지**해 S_PlayerDead 를 발행한다(불사 핵 차단).
/// </summary>
public class PlayerHpServerAuthorityTests
{
    private static global::Server.Room.Room NewRoom(params long[] ids)
    {
        var players = (ids.Length == 0 ? new long[] { 100 } : ids)
            .Select((id, i) => new PlayerInfo { UserId = id, Nickname = $"P{id}", SpawnIndex = i })
            .ToList();
        return new global::Server.Room.Room(1, players, NullLogger<global::Server.Room.Room>.Instance);
    }

    private static GameplayAttributeModifier[] Health(int amount)
        => new[] { new GameplayAttributeModifier(EGameplayAttribute.Health, amount, EModifierType.Additive) };

    [Fact]
    public void 입장하면_HP가_만피로_초기화된다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0f, 0f, 0f, 0f);

        var p = room.GetAllPlayerStates().Single();
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp, p.Hp);
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp, p.MaxHp);
        Assert.False(p.IsDowned);
    }

    [Fact]
    public void ApplyPlayerEffect는_데미지를_누적하고_HP0에서_최초_1회_다운한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0f, 0f, 0f, 0f);

        var a = room.ApplyPlayerEffect(100, Health(-30));
        Assert.Equal(70, a.NewHp);
        Assert.False(a.NewlyDowned);

        room.ApplyPlayerEffect(100, Health(-30)); // 40
        room.ApplyPlayerEffect(100, Health(-30)); // 10
        var dead = room.ApplyPlayerEffect(100, Health(-30)); // 0 이하 → 다운
        Assert.Equal(0, dead.NewHp);
        Assert.True(dead.NewlyDowned, "HP0 최초 도달 시 NewlyDowned");
        Assert.True(dead.FailClaimed, "솔로 전원다운 → 실패 claim");

        var again = room.ApplyPlayerEffect(100, Health(-30)); // 재적용
        Assert.False(again.NewlyDowned, "이미 다운 — 중복 발화 없음");
    }

    [Fact]
    public void 회복은_HP를_올리고_MaxHp로_클램프된다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0f, 0f, 0f, 0f);
        room.ApplyPlayerEffect(100, Health(-50)); // 50

        Assert.Equal(70, room.ApplyPlayerEffect(100, Health(+20)).NewHp);
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp, room.ApplyPlayerEffect(100, Health(+9999)).NewHp); // 클램프
    }

    [Fact]
    public void 서버가_몬스터_데미지로_HP0을_직접_감지해_C_PlayerDead_없이_S_PlayerDead를_발행한다()
    {
        var room = NewRoom(); // solo 100
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 슬라임(0,0,0) 사거리 안
        room.MarkJoined(100);                                // 입장 완료 = 라이브 타깃
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("slime", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        bool sawDead = false, sawFailed = false;
        long t = 1_000_000;
        for (int i = 0; i < 40 && !sawDead; i++)
        {
            var packets = room.TickMonsters(0.1f, t);
            if (packets.OfType<S_PlayerDead>().Any(p => p.UserId == 100)) sawDead = true;
            if (packets.OfType<S_DungeonFailed>().Any()) sawFailed = true;
            t += 1600; // 쿨다운(1500ms) 넘겨 매 틱 공격
        }

        Assert.True(sawDead, "서버가 자기 HP 로 사망을 감지해 S_PlayerDead 를 발행해야 한다(C_PlayerDead 미수신).");
        Assert.True(sawFailed, "솔로 전원 다운 → S_DungeonFailed.");
    }
}
