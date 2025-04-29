using System.IO;
using System.Text;

public class ChatPacket
{
    public string sender;
    public string receiver;
    public string message;
    public ChatType chatType;

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        WriteString(writer, sender);
        WriteString(writer, receiver);
        WriteString(writer, message);
        writer.Write((byte)chatType);

        return ms.ToArray();
    }

    public static ChatPacket Deserialize(byte[] buffer, int offset = 0)
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

    private void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write((ushort)bytes.Length); // uint16
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        ushort length = reader.ReadUInt16();
        byte[] bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }
}