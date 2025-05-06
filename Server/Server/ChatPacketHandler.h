#pragma once

#include "IPacketHandler.h"
#include "Packet.h"
#include "ChatPacket.h"
#include "GameRoom.h"

class ChatPacketHandler : public IPacketHandler
{
public:
    void Handle(PlayerSession* session, const std::vector<uint8_t>& data, size_t offset) override;
};

