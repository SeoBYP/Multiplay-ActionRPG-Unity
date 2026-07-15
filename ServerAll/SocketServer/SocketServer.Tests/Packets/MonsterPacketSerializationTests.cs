using MemoryPack;
using Shared.Packet.Packets;

namespace Server.Tests.Packets;

/// <summary>
/// M3 증분①: 몬스터 패킷(S_SpawnMonster / S_MonsterState / S_MonsterDead) 직렬화 계약 검증.
/// 전부 서버→클라 단방향. 몬스터는 서버 권위 NPC라 C_ 입력 패킷이 없다.
/// </summary>
public class MonsterPacketSerializationTests
{
    [Fact]
    public void S_SpawnMonster_라운드트립시_모든_필드가_보존된다()
    {
        var origin = new S_SpawnMonster
        {
            InstanceId = 3,
            MonsterId = "creepy_demon",
            PosX = 5.5f,
            PosY = 0f,
            PosZ = -2.25f,
            RotY = 90f,
            Hp = 30,
            MaxHp = 30,
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_SpawnMonster;

        Assert.NotNull(decoded);
        Assert.Equal(3, decoded!.InstanceId);
        Assert.Equal("creepy_demon", decoded.MonsterId);
        Assert.Equal(5.5f, decoded.PosX);
        Assert.Equal(0f, decoded.PosY);
        Assert.Equal(-2.25f, decoded.PosZ);
        Assert.Equal(90f, decoded.RotY);
        Assert.Equal(30, decoded.Hp);
        Assert.Equal(30, decoded.MaxHp);
    }

    [Fact]
    public void S_MonsterState_라운드트립시_모든_필드가_보존된다()
    {
        var origin = new S_MonsterState
        {
            InstanceId = 3,
            PosX = 7f,
            PosY = 0f,
            PosZ = 1f,
            RotY = 45f,
            Hp = 18,
            Phase = 2, // Chase
        };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_MonsterState;

        Assert.NotNull(decoded);
        Assert.Equal(3, decoded!.InstanceId);
        Assert.Equal(7f, decoded.PosX);
        Assert.Equal(0f, decoded.PosY);
        Assert.Equal(1f, decoded.PosZ);
        Assert.Equal(45f, decoded.RotY);
        Assert.Equal(18, decoded.Hp);
        Assert.Equal((byte)2, decoded.Phase);
    }

    [Fact]
    public void S_MonsterDead_라운드트립시_InstanceId가_보존된다()
    {
        var origin = new S_MonsterDead { InstanceId = 42 };

        byte[] bytes = MemoryPackSerializer.Serialize<Packet>(origin);
        var decoded = MemoryPackSerializer.Deserialize<Packet>(bytes) as S_MonsterDead;

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!.InstanceId);
    }

    [Fact]
    public void Union으로_역직렬화하면_구체타입이_복원된다()
    {
        Packet spawn = new S_SpawnMonster { InstanceId = 1, MonsterId = "creepy_demon" };
        Packet state = new S_MonsterState { InstanceId = 1, Phase = 1 };
        Packet dead = new S_MonsterDead { InstanceId = 1 };

        var decodedSpawn = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(spawn));
        var decodedState = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(state));
        var decodedDead = MemoryPackSerializer.Deserialize<Packet>(MemoryPackSerializer.Serialize(dead));

        Assert.IsType<S_SpawnMonster>(decodedSpawn);
        Assert.IsType<S_MonsterState>(decodedState);
        Assert.IsType<S_MonsterDead>(decodedDead);
    }
}
