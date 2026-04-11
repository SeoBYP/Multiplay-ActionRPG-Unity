using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
public partial class C_Attack : Packet
{
    public int SkillId { get; set; }
    public long TargetId { get; set; }
    public int DirectionX { get; set; }
    public int DirectionY { get; set; }
    public int DirectionZ { get; set; }
    public long TimeStamp { get; set; }
}

[MemoryPackable]
public partial class S_Attack : Packet
{
    public int SkillId { get; set; }
    public long AttackerId { get; set; }
    public long TargetId { get; set; }
    public int Damage { get; set; }
}