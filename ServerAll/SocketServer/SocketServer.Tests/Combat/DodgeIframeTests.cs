using Microsoft.Extensions.Logging.Abstractions;
using Script.System.GamePlayAbilitySystem;
using Server.Player;
using Shared.Infrastructure.Messages;
using Shared.Infrastructure.Spawn;
using Shared.Packet.Packets;

namespace Server.Tests.Combat;

/// <summary>
/// 2.6.1 회피(Dodge) 서버 권위 무적 프레임.
/// - PlayerState.TryBeginDodge: 쿨다운 게이트(C_Dodge 연사=영구 무적 치팅 차단).
/// - Room.TickMonsters: 무적 창 동안 몬스터 공격 피해 무시(빗나감).
/// </summary>
public class DodgeIframeTests
{
    private static global::Server.Room.Room NewRoom()
        => new(1,
            new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            NullLogger<global::Server.Room.Room>.Instance);

    [Fact]
    public void 회피_발동은_무적창을_부여하고_쿨다운_내_재발동은_거부된다()
    {
        var state = new PlayerState();
        const long t0 = 1_000_000;

        Assert.True(state.TryBeginDodge(t0));                       // 첫 발동 OK
        Assert.True(state.IsInvulnerableAt(t0 + DodgeConfig.IframeMs - 1)); // 무적 창 안
        Assert.False(state.IsInvulnerableAt(t0 + DodgeConfig.IframeMs));    // 창 만료

        Assert.False(state.TryBeginDodge(t0 + DodgeConfig.CooldownMs - 1)); // 쿨다운 내 거부
        Assert.True(state.TryBeginDodge(t0 + DodgeConfig.CooldownMs));      // 쿨다운 경과 후 OK
    }

    [Fact]
    public void 회피_무적_중인_플레이어는_몬스터_공격_피해를_무시한다()
    {
        var room = NewRoom();
        room.InitPlayerState(100, "A", 0, 0.5f, 0f, 0f, 0f); // 몬스터(0,0,0) 사거리 안
        room.MarkJoined(100);
        room.SpawnMonsters(
            new List<MonsterSpawnDef> { new("creepy_demon", 0f, 0f, 0f, 0f, 1, 0, Array.Empty<PatrolPoint>()) },
            new MapBounds(0f, 0f, 40f, 40f));

        const long t0 = 1_000_000;
        Assert.True(room.GetPlayerState(100)!.TryBeginDodge(t0)); // t0+IframeMs 까지 무적

        // 무적 창 안: 몬스터 공격이 빗나감 — effect 없음, 서버 HP 유지.
        var p1 = room.TickMonsters(0.1f, t0 + 100);
        Assert.Empty(p1.OfType<S_ApplyEffect>());
        Assert.Equal(global::Server.Room.Room.DefaultMaxHp, room.GetPlayerState(100)!.Hp);

        // 무적 만료 + 몬스터 쿨다운(1500ms) 경과 → 다시 피해. (slime 은 데미지+슬로우 2효과 — 데미지만 특정)
        var p2 = room.TickMonsters(0.1f, t0 + 2000);
        Assert.Single(p2.OfType<S_ApplyEffect>().Where(e => e.EffectId == "monster_attack_dmg"));
        Assert.True(room.GetPlayerState(100)!.Hp < global::Server.Room.Room.DefaultMaxHp);
    }
}
