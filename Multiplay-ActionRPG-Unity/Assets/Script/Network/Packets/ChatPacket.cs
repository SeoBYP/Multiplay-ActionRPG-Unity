using System.IO;
using System.Text;

namespace Game.Network
{
    public enum ChatType : byte
    {
        GLOBAL = 0,
        WHISPER = 1,
        SYSTEM = 2
    }

    public class ChatPacket : Packet
    {
        public string sender;
        public string receiver;
        public string message;
        public ChatType chatType;

        public ChatPacket() : base(PacketType.CHAT)
        {
            
        }

        public override byte[] Serialize()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            WriteString(writer, sender);
            WriteString(writer, receiver);
            WriteString(writer, message);
            writer.Write((byte)chatType);

            return ms.ToArray();
        }

        public ChatPacket Deserialize(byte[] buffer, int offset = 0)
        {
            using var ms = new MemoryStream(buffer, offset, buffer.Length - offset);
            using var reader = new BinaryReader(ms, Encoding.UTF8);

            var pkt = new ChatPacket();
            pkt.sender = ReadString(reader);
            pkt.receiver = ReadString(reader);
            pkt.message = ReadString(reader);
            pkt.chatType = (ChatType)reader.ReadByte();
            return pkt;
        }
    }
}