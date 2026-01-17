namespace Server.Packet;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PacketHandlerAttribute : Attribute
{
    public string PayloadCaseName { get; }
    public PacketHandlerAttribute(string payloadCaseName) => PayloadCaseName = payloadCaseName;
}