using MemoryPack;

namespace Shared.Packet.Packets;

// AuthPackets.cs
[MemoryPackable]
public partial class C_Auth : Packet
{
    public long UserId { get; set; }
    public long RoomId { get; set; }
}

[MemoryPackable]
public partial class S_Auth : Packet
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
