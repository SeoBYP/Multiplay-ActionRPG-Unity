using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public enum PacketType : uint
{
    CHAT = 1,
    LOGIN = 2,
    SYSTEM = 3,
    UNKNOWN = 255
}

public enum ChatType : byte
{
    GLOBAL = 0,
    WHISPER = 1,
    SYSTEM = 2
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PacketHeader
{
    public PacketType type;
    public uint size;

    public static byte[] Serialize(PacketHeader header)
    {
        int size = Marshal.SizeOf(typeof(PacketHeader));
        byte[] buffer = new byte[size];
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(header, ptr, true);
        Marshal.Copy(ptr, buffer, 0, size);
        Marshal.FreeHGlobal(ptr);
        return buffer;
    }

    public static PacketHeader Deserialize(byte[] buffer, int offset = 0)
    {
        int size = Marshal.SizeOf(typeof(PacketHeader));
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Marshal.Copy(buffer, offset, ptr, size);
        PacketHeader result = Marshal.PtrToStructure<PacketHeader>(ptr);
        Marshal.FreeHGlobal(ptr);
        return result;
    }
}