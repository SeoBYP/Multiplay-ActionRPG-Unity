using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Server.Player;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Combat;

/// <summary>
/// CA-3 서버 권위 적중 판정: 시전자 위치/yaw 기준 hitbox로 대상 적중을 재계산.
/// </summary>
public class CombatHandlerTests
{
    private static PlayerState Player(long id, float x, float z, float rotY = 0f)
        => new() { UserId = id, PosX = x, PosY = 0f, PosZ = z, RotY = rotY };

    [Fact]
    public void 정면_hitbox_안의_대상은_적중이다()
    {
        var skill = CombatHandler.ResolveSkill(0)!;       // basic_swing: 정면(+Z) 박스
        var attacker = Player(100, 0, 0, rotY: 0f);
        var candidates = new List<PlayerState> { attacker, Player(200, 0, 1f) };

        var hits = CombatHandler.SelectHitTargets(skill, attacker, candidates, CombatHandler.TargetRadius);

        Assert.Contains(200L, hits);
        Assert.DoesNotContain(100L, hits); // 자기 자신 제외
    }

    [Fact]
    public void 뒤_또는_먼_대상은_적중하지_않는다()
    {
        var skill = CombatHandler.ResolveSkill(0)!;
        var attacker = Player(100, 0, 0, rotY: 0f);
        var candidates = new List<PlayerState>
        {
            attacker,
            Player(201, 0, -1f),  // 뒤
            Player(202, 0, 3f),   // 정면이지만 너무 멀다
        };

        var hits = CombatHandler.SelectHitTargets(skill, attacker, candidates, CombatHandler.TargetRadius);

        Assert.Empty(hits);
    }

    [Fact]
    public void yaw_180도면_뒤의_대상이_정면이_되어_적중한다()
    {
        var skill = CombatHandler.ResolveSkill(0)!;
        var attacker = Player(100, 0, 0, rotY: 180f);
        var candidates = new List<PlayerState> { attacker, Player(200, 0, -1f) };

        var hits = CombatHandler.SelectHitTargets(skill, attacker, candidates, CombatHandler.TargetRadius);

        Assert.Contains(200L, hits);
    }

    [Fact]
    public void Room_NextEffectInstanceId는_1부터_단조증가한다()
    {
        var room = new global::Server.Room.Room(
            roomId: 1,
            expectedUserIds: new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            logger: NullLogger<global::Server.Room.Room>.Instance);

        Assert.Equal(1, room.NextEffectInstanceId());
        Assert.Equal(2, room.NextEffectInstanceId());
        Assert.Equal(3, room.NextEffectInstanceId());
    }
}
