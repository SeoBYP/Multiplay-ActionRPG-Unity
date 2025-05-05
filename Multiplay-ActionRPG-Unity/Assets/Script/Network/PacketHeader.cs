using System.IO;
using System.Runtime.InteropServices;

namespace Game.Network
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PacketHeader
    {
        public PacketType type;
        public uint size;

        public static byte[] Serialize(PacketHeader header)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)header.type); // enum은 uint32로 캐스팅
            writer.Write(header.size);
            return ms.ToArray();
        }

        public static PacketHeader Deserialize(byte[] buffer, int offset = 0)
        {
            const int headerSize = 8;
            using var ms = new MemoryStream(buffer, offset, headerSize);
            using var reader = new BinaryReader(ms);
            var type = (PacketType)reader.ReadUInt32();
            var size = reader.ReadUInt32();
            return new PacketHeader { type = type, size = size };
        }
    }
}