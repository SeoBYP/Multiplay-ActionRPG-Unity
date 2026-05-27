using MemoryPack;

namespace Shared.Packet.Packets;

[MemoryPackable]
public partial class C_Ping : Packet
{
    public bool IsHealthy { get; set; } = false;
}

[MemoryPackable]
public partial class S_Pong : Packet
{
    public bool IsHealthy { get; set; } = false;
}

