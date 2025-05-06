using System.IO;
using System.Text;

namespace Game.Network
{
    public class C_SetNicknamePacket : Packet
    {
        public string nickname;

        public C_SetNicknamePacket() : base(PacketType.SET_NICKNAME_C2S) { }

        public override byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);
            WriteString(writer, nickname);
            return ms.ToArray();
        }

        public static C_SetNicknamePacket Deserialize(byte[] buffer, int offset = 0)
        {
            using var ms = new MemoryStream(buffer, offset, buffer.Length - offset);
            using var reader = new BinaryReader(ms, Encoding.UTF8);
            var pkt = new C_SetNicknamePacket();
            pkt.nickname = pkt.ReadString(reader);
            return pkt;
        }
    }
}