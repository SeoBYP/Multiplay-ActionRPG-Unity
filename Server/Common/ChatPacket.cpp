#include "pch.h"
#include "ChatPacket.h"

ChatPacket::ChatPacket()
    : Packet(PacketType::CHAT) {
}

ChatPacket::ChatPacket(std::string s, ChatType t, std::string msg, std::string r) // 정의 – 기본값 제거됨
    : Packet(PacketType::CHAT), sender(s), receiver(r), chatType(t), message(msg) {
    setSize(static_cast<uint32_t>(s.size() + r.size() + msg.size() + sizeof(ChatType)));
}
