using MemoryPack;
using Shared.Packet.Packets;

namespace Server.Tests.Packets;

/// <summary>
/// EF-2c: Effect 동기화 패킷(S_ApplyEffect / S_RemoveEffect) 직렬화 계약 검증.
/// 남은시간은 보내지 않는다 — 권위 StartTick + 공유 카탈로그 EffectId로 양측이 duration을 산출한다.
/// </summary>
public class EffectPacketSerializationTests
{
    [Fact]
    public void S_ApplyEffect_라운드트립시_모든_필드가_보존된다()
    {
        var origin = new S_ApplyEffect
        {
            InstanceId = 7,
            EffectId = "atk_up_20",
            TargetId = 100,
            SourceId = 200,
            StartTick = 1_234_567,
            Stacks = 2,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_ApplyEffect;

        Assert.NotNull(decoded);
        Assert.Equal(7, decoded!.InstanceId);
        Assert.Equal("atk_up_20", decoded.EffectId);
        Assert.Equal(100, decoded.TargetId);
        Assert.Equal(200, decoded.SourceId);
        Assert.Equal(1_234_567, decoded.StartTick);
        Assert.Equal(2, decoded.Stacks);
    }

    [Fact]
    public void S_RemoveEffect_라운드트립시_InstanceId가_보존된다()
    {
        var origin = new S_RemoveEffect { InstanceId = 42 };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_RemoveEffect;

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.InstanceId);
    }

    [Fact]
    public void Union으로_역직렬화하면_구체타입이_복원된다()
    {
        Packet apply = new S_ApplyEffect { InstanceId = 1, EffectId = "def_down_10" };
        Packet remove = new S_RemoveEffect { InstanceId = 1 };

        var decodedApply = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(apply));
        var decodedRemove = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(remove));

        Assert.IsType<S_ApplyEffect>(decodedApply);
        Assert.IsType<S_RemoveEffect>(decodedRemove);
    }
}
