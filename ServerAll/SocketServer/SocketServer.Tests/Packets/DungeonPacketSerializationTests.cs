using MemoryPack;
using Shared.Packet.Packets;

namespace Server.Tests.Packets;

/// <summary>
/// 던전 라이프사이클 패킷 직렬화 계약. 특히 ⓔ-2 S_PlayerDead(Union 1823, 원격 다운 가시성):
/// Union 등록 누락은 런타임 역직렬화 오류 1순위라 라운드트립으로 박제한다.
/// </summary>
public class DungeonPacketSerializationTests
{
    [Fact]
    public void S_PlayerDead_라운드트립시_UserId가_보존된다()
    {
        var origin = new S_PlayerDead { UserId = 4242 };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_PlayerDead;

        Assert.NotNull(decoded);
        Assert.Equal(4242, decoded!.UserId);
    }

    [Fact]
    public void Union으로_역직렬화하면_S_PlayerDead_구체타입이_복원된다()
    {
        Packet dead = new S_PlayerDead { UserId = 1 };

        var decoded = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(dead));

        Assert.IsType<S_PlayerDead>(decoded);
    }
}
