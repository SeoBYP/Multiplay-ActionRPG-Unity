using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
public partial class C_Move : Packet
{
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public long TimeStamp { get; set; }
}

[MemoryPackable]
public partial class S_Move : Packet
{
    public long UserId { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public float RotY { get; set; }
    public long TimeStamp { get; set; }
}
