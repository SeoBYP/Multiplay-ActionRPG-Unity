using MemoryPack;
using Script.System.GamePlayAbilitySystem;
using Shared.Packet.Packets;

namespace Server.Tests.Packets;

/// <summary>
/// AC 증분2: Actor 통합 발동 연출 신호 `S_AbilityActivated`(Union 1604) 직렬화 계약 검증.
/// ActorId 부호 규약(플레이어=양수 UserId / 몬스터=음수 -InstanceId)이 왕복에서 보존되어야
/// 클라 ActorRegistry 가 올바른 대상에 Cue 를 재생한다. actor-combat-architecture §4.
/// </summary>
public class AbilityActivatedPacketSerializationTests
{
    [Fact]
    public void S_AbilityActivated_라운드트립시_ActorId와_SkillId가_보존된다()
    {
        var origin = new S_AbilityActivated
        {
            ActorId = ActorIds.FromPlayer(100),
            SkillId = 3,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_AbilityActivated;

        Assert.NotNull(decoded);
        Assert.Equal(100L, decoded!.ActorId);
        Assert.Equal(3, decoded.SkillId);
    }

    [Fact]
    public void 몬스터_ActorId_음수도_왕복에서_보존된다()
    {
        var origin = new S_AbilityActivated
        {
            ActorId = ActorIds.FromMonster(7), // -7
            SkillId = 0,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_AbilityActivated;

        Assert.NotNull(decoded);
        Assert.Equal(-7L, decoded!.ActorId);
        Assert.True(ActorIds.IsMonster(decoded.ActorId));
        Assert.Equal(7, ActorIds.ToMonsterInstanceId(decoded.ActorId));
    }

    [Fact]
    public void Union_1604로_역직렬화하면_구체타입이_복원된다()
    {
        Packet packet = new S_AbilityActivated { ActorId = -1, SkillId = 0 };

        var decoded = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(packet));

        Assert.IsType<S_AbilityActivated>(decoded);
    }
}
