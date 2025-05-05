namespace Game.Network
{
    public interface IPacketHandler
    {
        void Handle(Packet packet);
    }
}