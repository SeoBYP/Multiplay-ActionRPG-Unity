using System.IO;
using System.Text;

namespace Game.Network
{
    public abstract class Packet
    {
        private PacketType _type;
        
        public PacketType PacketType => _type;
        
        public Packet(PacketType type)
        {
            _type = type;
        }
        public abstract byte[] Serialize();
        protected void WriteString(BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)bytes.Length); // uint16
            writer.Write(bytes);
        }
        
        protected string ReadString(BinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}