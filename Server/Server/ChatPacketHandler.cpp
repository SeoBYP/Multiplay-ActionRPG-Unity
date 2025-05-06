#include "ChatPacketHandler.h"

void ChatPacketHandler::Handle(PlayerSession* session, const std::vector<uint8_t>& data, size_t offset)
{
    ChatPacket pkt = ChatPacket::Deserialize(data, offset);
    auto room = session->GetGameRoom();
    if (!room) return;

    ChatType type = static_cast<ChatType>(pkt.chatType);

    std::cout << "[ChatType: " << static_cast<int>(type) << "] "
        << pkt.sender << " → " << pkt.receiver << " : " << pkt.message << std::endl;


    auto body = pkt.Serialize();
    PacketHeader header{ PacketType::CHAT, static_cast<uint32_t>(body.size()) };

    std::vector<uint8_t> fullPacket(sizeof(PacketHeader));
    std::memcpy(fullPacket.data(), &header, sizeof(PacketHeader));
    fullPacket.insert(fullPacket.end(), body.begin(), body.end());

    switch (type)
    {
    case ChatType::GLOBAL:
    {
        room->Broadcast(fullPacket);
        break;
    }

    case ChatType::WHISPER:
    {
        auto target = room->FindPlayerByNick(pkt.receiver);
        if (target)
        {
            target->Send(fullPacket);    // 대상에게 메시지 전송
            session->Send(fullPacket);   // 본인에게도 회신
        }
        else
        {
            // 귓속말 대상이 없을 경우 System 메시지 전송
            ChatPacket errorMsg;
            errorMsg.sender = "System";
            errorMsg.receiver = pkt.sender;
            errorMsg.message = "유저 " + pkt.receiver + " 닉네임을 찾을 수 없습니다.";
            errorMsg.chatType = static_cast<uint8_t>(ChatType::SYSTEM);

            auto errBody = errorMsg.Serialize();
            PacketHeader errHeader{ PacketType::CHAT, static_cast<uint32_t>(errBody.size()) };

            std::vector<uint8_t> errPacket(sizeof(PacketHeader));
            std::memcpy(errPacket.data(), &errHeader, sizeof(PacketHeader));
            errPacket.insert(errPacket.end(), errBody.begin(), errBody.end());

            session->Send(errPacket);
        }
        break;
    }

    case ChatType::SYSTEM:
    {
        // 서버 발송용 메시지 타입, 클라이언트에서만 수신
        std::cerr << "[Chat] SYSTEM 타입은 클라이언트 전용입니다.\n";
        break;
    }

    default:
    {
        std::cerr << "[Chat] 알 수 없는 ChatType: " << static_cast<int>(pkt.chatType) << "\n";
        break;
    }
    }
}
