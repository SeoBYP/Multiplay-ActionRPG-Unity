using MemoryPack;

namespace Shared.Packet;
using Packets;

public static class PacketSerializer
{
    public static byte[] Serialize(Packet packet)
        => MemoryPackSerializer.Serialize<Packet>(packet);

    public static Packet? Deserialize(byte[] data)
        => MemoryPackSerializer.Deserialize<Packet>(data);

    public static Packet? Deserialize(ReadOnlySpan<byte> data)
        => MemoryPackSerializer.Deserialize<Packet>(data);
}