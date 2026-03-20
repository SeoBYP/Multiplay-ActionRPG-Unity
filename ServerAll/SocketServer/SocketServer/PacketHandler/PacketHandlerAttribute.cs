namespace Server.PacketHandler;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PacketHandlerAttribute : Attribute
{
    public Type PacketType { get; }

    public PacketHandlerAttribute(Type packetType)
    {
        PacketType = packetType;
    }
}