using UnityEngine;
using GUI = Game.Managers.GUI;

namespace Game.Network
{
    public class ChatPacketHandler : IPacketHandler
    {
        public void Handle(Packet packet)
        {
            if (packet is ChatPacket chat)
            {
                _ = GUI.Get<MainHUD>().ChatBox.AppendChatMessage(chat.sender,chat.message);
            }
        }
    }
}