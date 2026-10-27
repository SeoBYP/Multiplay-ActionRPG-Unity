using Microsoft.Extensions.Logging.Abstractions;
using Server.PacketHandler.Handler;
using Server.Actors;
using Shared.Infrastructure.Messages;

namespace Server.Tests.Combat;

/// <summary>
/// 서버 권위 적중 판정: 시전자 위치/yaw 기준 hitbox로 대상 적중을 재계산.
/// </summary>
public class CombatHandlerTests
{
    private static PlayerActor Player(long id, float x, float z, float rotY = 0f)
        => new(id) { PosX = x, PosY = 0f, PosZ = z, RotY = rotY };

    // 아군 오사는 없다 — 플레이어 hitbox 적중 판정(SelectHitTargets)을 두면 클라 HP 만 깎고 서버 PlayerActor 의 HP 는 안 건드려
    // 서버가 사망을 감지하지 못한다(S_PlayerDead 미발행).
    // 회귀 감시는 E2E `RawSocket_정면의_아군을_공격해도_피해가_들어가지_않는다` 가 맡는다.
    // 몬스터 적중 판정은 ApplyAttackToMonsters 경로(MonsterDamageTests).

    [Fact]
    public void Room_NextEffectInstanceId는_1부터_단조증가한다()
    {
        var room = new global::Server.Room.Room(
            roomId: 1,
            participants: new List<PlayerInfo> { new() { UserId = 100, Nickname = "A", SpawnIndex = 0 } },
            logger: NullLogger<global::Server.Room.Room>.Instance);

        Assert.Equal(1, room.NextEffectInstanceId());
        Assert.Equal(2, room.NextEffectInstanceId());
        Assert.Equal(3, room.NextEffectInstanceId());
    }
}
