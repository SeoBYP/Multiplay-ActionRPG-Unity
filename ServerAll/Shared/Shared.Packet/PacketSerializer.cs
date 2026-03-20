using MemoryPack;

namespace Shared.Packet;
using Packets;

public static class PacketSerializer
{
    // 직렬화: Packet → byte[]
    public static byte[] Serialize(Packet packet)
        => MemoryPackSerializer.Serialize<Packet>(packet);

    // 역직렬화: byte[] → Packet
    public static Packet? Deserialize(byte[] data)
        => MemoryPackSerializer.Deserialize<Packet>(data);

    public static Packet? Deserialize(ReadOnlySpan<byte> data)
        => MemoryPackSerializer.Deserialize<Packet>(data);
}