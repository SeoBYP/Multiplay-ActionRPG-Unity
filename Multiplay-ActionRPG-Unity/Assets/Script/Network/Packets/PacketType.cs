namespace Game.Network
{
    public enum PacketType : uint
    {
        CHAT = 1,
        LOGIN = 2,
        SYSTEM = 3,
        UNKNOWN = 255
    }
}