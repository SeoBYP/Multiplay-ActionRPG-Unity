namespace Game.Network
{
    public enum PacketType : uint
    {
        CHAT = 1,
        SET_NICKNAME_C2S = 2,
        SET_NICKNAME_S2C = 3,
        SYSTEM = 4,
        UNKNOWN = 255
    }
}