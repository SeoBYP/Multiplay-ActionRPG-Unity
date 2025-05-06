using System.IO;
using System.Text;

namespace Game.Network
{
    public class S_SetNicknamePacket : Packet
    {
        public bool success;
        public string message;

        public S_SetNicknamePacket() : base(PacketType.SET_NICKNAME_S2C) { }

        public override byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);
            writer.Write(success);
            WriteString(writer, message);
            return ms.ToArray();
        }

        public static S_SetNicknamePacket Deserialize(byte[] buffer, int offset = 0)
        {
            using var ms = new MemoryStream(buffer, offset, buffer.Length - offset);
            using var reader = new BinaryReader(ms, Encoding.UTF8);
            var pkt = new S_SetNicknamePacket();
            pkt.success = reader.ReadBoolean();
            pkt.message = pkt.ReadString(reader);
            return pkt;
        }

    }
}