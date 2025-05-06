#pragma once
#include "IPacketHandler.h"

class SetNicknamePacketHandler : public IPacketHandler
{
public:
    void Handle(PlayerSession* session, const std::vector<uint8_t>& data, size_t offset) override;
};
