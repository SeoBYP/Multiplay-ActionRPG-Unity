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

    // 아군 오사 폐지(2026-08-22)로 플레이어 hitbox 적중 판정(SelectHitTargets)은 제거됐다.
    // 근거: 그 경로는 클라 HP 만 깎고 서버 PlayerState.Hp 를 안 건드려, 파티가 붙어 싸우면
    // 클라가 먼저 0 에 도달해 서버가 사망을 감지하지 못했다(S_PlayerDead 미발행 → 원격 사망 애니 없음).
    // 회귀 감시는 E2E `RawSocket_정면의_아군을_공격해도_피해가_들어가지_않는다` 가 맡는다.
    // 몬스터 적중 판정은 ApplyAttackToMonsters 경로에 그대로 남아 있다(MonsterDamageTests).

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
